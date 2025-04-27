#region

using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

#endregion

public class Comment
{
    public int CommentID { get; set; }
    public int UserID { get; set; }
    public string UserName { get; set; }
    public int PostID { get; set; }
    public string CreatedIn { get; set; }
    public string Text { get; set; }
    public string ModifiedIn { get; set; }
}

public class Post
{
    public PostCategory PostCategory { get; set; }
    public int PostID { get; set; }
    public int UserID { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Attachment { get; set; }
    public string CreatedIn { get; set; }
    public int PostCategoryID { get; set; }
    public string UserName { get; set; }
    public List<Comment> Comments { get; set; }
    public int CountVotes { get; set; }
    public int CountComments { get; set; }
    public int VoteValue { get; set; }
    public bool AllowComments { get; set; }
}

public class PostCategory
{
    public int PostCategoryID { get; set; }
    public string Title { get; set; }
}

namespace api.garagecom.Controllers
{
    [ActionFilter]
    [Route("api/[controller]")]
    public class PostsController : Controller
    {
        #region ComboBox

        [HttpGet("GetPostCategories")]
        public ApiResponse GetPostCategories()
        {
            var apiResponse = new ApiResponse();
            try
            {
                var postCategories = new List<PostCategory>();
                var sql = @"SELECT PostCategoryID, Title
                            FROM PostCategories";
                MySqlParameter[] parameters = [];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        postCategories.Add(new PostCategory
                        {
                            PostCategoryID = reader["PostCategoryID"] != DBNull.Value
                                ? Convert.ToInt32(reader["PostCategoryID"])
                                : -1,
                            Title = (reader["Title"] != DBNull.Value ? reader["Title"].ToString() : "")!
                        });
                }

                apiResponse.Parameters["PostCategories"] = postCategories;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        #endregion


        [HttpGet("GetPostAttachment")]
        public async Task<FileResult> GetPostAttachment(string fileName)
        {
            var file = await S3Helper.DownloadAttachmentAsync(fileName, "Images/Posts/");
            return File(file, "application/octet-stream", fileName);
        }

        [HttpPost("SetPostAttachment")]
        public ApiResponse SetPostAttachment(int postId, IFormFile file)
        {
            var attachmentName = $"{postId}_{Guid.NewGuid().ToString()}";
            var task = new Task(async void () =>
            {
                var status = await S3Helper.UploadAttachmentAsync(file, attachmentName, "Images/Posts/");
                if (!status) return;
                var sql = @"UPDATE Posts
                            SET Attachment = @Attachment
                            WHERE PostID = @PostID";
                MySqlParameter[] parameters =
                [
                    new("Attachment", attachmentName),
                    new("PostID", postId)
                ];
                DatabaseHelper.ExecuteNonQuery(sql, parameters);
            });
            task.Start();
            return new ApiResponse
            {
                Succeeded = true,
                Parameters =
                {
                    ["AttachmentName"] = attachmentName
                }
            };
        }

        #region Posts

        [HttpGet("GetPosts")]
        public ApiResponse GetPosts(int[] categoryId)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                var posts = new List<Post>();
                var sql =
                    @"WITH VoteData AS (
    SELECT
        PostID,
        SUM(Value) AS VoteCount,
        MAX(
                CASE
                    WHEN UserID = @UserID
                        AND StatusID = (
                            SELECT StatusID
                            FROM Statuses
                            WHERE Status = 'Active'
                        )
                        THEN Value
                    END
        ) AS UserVoteValue
    FROM Votes
    GROUP BY PostID
),
     CommentData AS (
         SELECT
             PostID,
             COUNT(*) AS CommentCount
         FROM Comments
         GROUP BY PostID
     )
SELECT
    P.PostID,
    G.UserName,
    P.AllowComments,
    P.UserID,
    P.Title,
    P.Description,
    P.Attachment,
    P.CreatedIn,
    P.PostCategoryID,
    C.Title            AS CategoryTitle,
    COALESCE(V.VoteCount, 0)                AS VoteCount,
    COALESCE(CD.CommentCount, 0)            AS CommentCount,
    V.UserVoteValue                        AS UserVoteValue
FROM Posts P
         INNER JOIN PostCategories C
                    ON C.PostCategoryID = P.PostCategoryID
         INNER JOIN GeneralInformation G
                    ON G.UserID = P.UserID
         INNER JOIN Statuses S
                    ON S.StatusID = P.StatusID
         LEFT JOIN VoteData V
                   ON V.PostID = P.PostID
         LEFT JOIN CommentData CD
                   ON CD.PostID = P.PostID
WHERE
    (@PostCategoryIDs = ''
        OR FIND_IN_SET(P.PostCategoryID, @PostCategoryID))
  AND S.Status = 'Active'
ORDER BY P.CreatedIn DESC;";
                MySqlParameter[] parameters =
                [
                    new("PostCategoryID", string.Join(",", categoryId)),
                    new("UserID", userId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        posts.Add(new Post
                        {
                            PostID = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1,
                            PostCategoryID = reader["PostID"] != DBNull.Value
                                ? Convert.ToInt32(reader["PostCategoryID"])
                                : -1,
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            Title = (reader["Title"] != DBNull.Value ? reader["Title"].ToString() : "")!,

                            UserName = (reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "")!,
                            Description =
                                (reader["Description"] != DBNull.Value ? reader["Description"].ToString() : "")!,
                            Attachment = (reader["Attachment"] != DBNull.Value ? reader["Attachment"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            AllowComments = reader["AllowComments"] == DBNull.Value ||
                                            Convert.ToBoolean(reader["AllowComments"]),
                            PostCategory = new PostCategory
                            {
                                PostCategoryID = reader["PostCategoryID"] != DBNull.Value
                                    ? Convert.ToInt32(reader["PostCategoryID"])
                                    : -1,
                                Title = (reader["CategoryTitle"] != DBNull.Value
                                    ? reader["CategoryTitle"].ToString()
                                    : "")!
                            },
                            CountVotes = reader["VoteCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["VoteCount"]),
                            CountComments = reader["CommentCount"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["CommentCount"]),
                            VoteValue = reader["UserVoteValue"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["UserVoteValue"]),
                            Comments = []
                        });
                }

                sql =
                    @"SELECT CommentID, Comments.UserID, Comments.PostID, Text, Comments.CreatedIn, Comments.ModifiedIn
                            FROM Comments
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID AND Posts.PostCategoryID IN (@PostCategoryID, -10)
                            INNER JOIN Statuses SC ON SC.StatusID = Comments.StatusID
                            INNER JOIN Statuses SP ON SP.StatusID = Posts.StatusID
                            WHERE SP.Status = 'Active' AND SC.Status = 'Active'";
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                    {
                        var postId = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1;
                        var post = posts.FirstOrDefault(p => p.PostID == postId);
                        post?.Comments.Add(new Comment
                        {
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            PostID = postId,
                            Text = (reader["Text"] != DBNull.Value ? reader["Text"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            ModifiedIn = reader["ModifiedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["ModifiedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : ""
                        });
                    }
                }

                apiResponse.Parameters["Posts"] = posts;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpPost("SetPost")]
        public async Task<ApiResponse> SetPost(string title, int postCategoryId, string description)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"
SELECT StatusID INTO @StatusID
FROM Statuses S
WHERE S.Status = @StatusName;
INSERT INTO Posts (UserID, Title, PostCategoryID, CreatedIn, StatusID, Description)
                            VALUES (@UserID, @Title, @PostCategoryID, NOW(), @StatusID, @Description);
                            SELECT LAST_INSERT_ID();";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId),
                    new("Title", title),
                    new("PostCategoryID", postCategoryId),
                    new("Description", description),
                    new("StatusName", "Active")
                ];

                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);

                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpPut]
        public ApiResponse UpdatePost(int postId, string title, string description)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"UPDATE Posts
                            SET Title = @Title,
                                Description = @Description,
                                ModifiedIn = NOW()
                            WHERE PostID = @PostID";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId),
                    new("Title", title),
                    new("Description", description)
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpDelete("DeletePost")]
        public ApiResponse DeletePost(int postId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"
SELECT StatusID INTO @StatusID
FROM Statuses S
WHERE S.Status = @Status;
UPDATE Posts
                            SET StatusID = @StatusID,
                                ModifiedIn = NOW()
                            WHERE PostID = @PostID";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId),
                    new("Status", "InActive")
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        #endregion

        #region Comments

        [HttpPost("SetComment")]
        public ApiResponse SetComment(int postId, string text)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"
SELECT StatusID INTO @StatusID
FROM Statuses S
WHERE S.Status = @Status;
INSERT INTO Comments (UserID, PostID, Text, CreatedIn, StatusID)
                            VALUES (@UserID, @PostID, @Text, NOW(), @StatusID)";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId),
                    new("PostID", postId),
                    new("Text", text),
                    new("Status", "Active")
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                var task = new Task(() =>
                {
                    sql = @"
                        SELECT UserID
                        INTO @PostUserID
                        FROM Posts P
                        WHERE P.PostID = @PostID
                        ORDER BY P.CreatedIn DESC
                        LIMIT 1;
                        SELECT DeviceToken
                        FROM Logins L
                        WHERE L.UserID = @PostUserID
                        ORDER BY L.CreatedIn DESC
                        LIMIT 1;";
                    var postUserId = -1;
                    var deviceToken = "";
                    parameters =
                    [
                        new MySqlParameter("PostID", postId)
                    ];
                    var apiResponseScalar = DatabaseHelper.ExecuteScalar(sql, parameters);
                    if (apiResponseScalar.Succeeded)
                    {
                        postUserId = Convert.ToInt32(apiResponseScalar.Parameters["PostUserID"]);
                        deviceToken = apiResponseScalar.Parameters["DeviceToken"].ToString();
                    }

                    if (postUserId != -1 && deviceToken != "")
                    {
                        var notification = new NotificationRequest
                        {
                            Title = "New Comment On Your Post",
                            Body = text
                        };
                        var notificationResponse = NotificationHelper.SendNotification(notification);
                    }
                });
                task.Start();
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpPost("UpdateComment")]
        public ApiResponse UpdateComment(int commentId, string text)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"UPDATE Comments
                            SET Text = @Text,
                                ModifiedIn = NOW()
                            WHERE CommentID = @CommentID";
                MySqlParameter[] parameters =
                [
                    new("CommentID", commentId),
                    new("Text", text)
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpPost("DeleteComment")]
        public ApiResponse DeleteComment(int commentId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"
SELECT StatusID INTO @StatusID
FROM Statuses S
WHERE S.Status = @Status;
UPDATE Comments
                            SET StatusID = @StatusID,
                                ModifiedIn = NOW()
                            WHERE CommentID = @CommentID";
                MySqlParameter[] parameters =
                [
                    new("CommentID", commentId),
                    new("Status", "InActive")
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetCommentsByPostId")]
        public ApiResponse GetCommentsByPostId(int postId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var comments = new List<Comment>();
                var sql =
                    @"SELECT CommentID, Comments.UserID, GeneralInformation.UserName, Comments.PostID, Text, Comments.CreatedIn AS CreatedIn, Comments.ModifiedIn
                            FROM Comments
                                INNER JOIN GeneralInformation ON GeneralInformation.UserID = Comments.UserID
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID AND Posts.PostID = @PostID
                            INNER JOIN Statuses SC ON SC.StatusID = Comments.StatusID
                            INNER JOIN Statuses SP ON SP.StatusID = Posts.StatusID
                            WHERE SC.Status = 'Active' AND SP.Status = 'Active'";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        comments.Add(new Comment
                        {
                            CommentID = reader["CommentID"] != DBNull.Value ? Convert.ToInt32(reader["CommentID"]) : -1,
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            PostID = postId,
                            Text = (reader["Text"] != DBNull.Value ? reader["Text"].ToString() : "")!,
                            UserName = (reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            ModifiedIn = reader["ModifiedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["ModifiedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : ""
                        });
                }

                apiResponse.Parameters["Comments"] = comments;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetComment")]
        public ApiResponse GetComment(int commentId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var comment = new Comment();
                var sql =
                    @"SELECT CommentID, Comments.UserID, GeneralInformation.UserName, Comments.PostID, Text, Comments.CreatedIn AS CreatedIn, Comments.ModifiedIn, Comments.PostID
                            FROM Comments
                            INNER JOIN GeneralInformation ON GeneralInformation.UserID = Comments.UserID
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID
                            INNER JOIN Statuses SC ON SC.StatusID = Comments.StatusID
                            INNER JOIN Statuses SP ON SP.StatusID = Posts.StatusID
                            WHERE SC.Status = 'Active' AND SP.Status = 'Active' AND CommentID = @CommentID";
                MySqlParameter[] parameters =
                [
                    new("CommentID", commentId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        comment = new Comment
                        {
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            CommentID = commentId,
                            PostID = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1,
                            Text = (reader["Text"] != DBNull.Value ? reader["Text"].ToString() : "")!,
                            UserName = (reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            ModifiedIn = reader["ModifiedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["ModifiedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : ""
                        };
                }

                apiResponse.Parameters["Comment"] = comment;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        #endregion

        #region Votes

        [HttpPost("SetVote")]
        public ApiResponse SetVote(int postId, int value)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"SELECT StatusID INTO @StatusID FROM Statuses S WHERE S.Status = 'Active';
                        INSERT INTO Votes (UserID, PostID, CreatedIn, Value, StatusID)
                            VALUES (@UserID, @PostID, NOW(), @UpVote, @StatusID)";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId),
                    new("PostID", postId),
                    new("UpVote", value)
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpPost("DeleteVote")]
        public ApiResponse DeleteVote(int postId)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"SELECT StatusID INTO @StatusID FROM Statuses S WHERE S.Status = 'InActive';
                        UPDATE Votes
                            SET StatusID = @StatusID,
                                ModifiedIn = NOW()
                            WHERE UserID = @UserID AND PostID = @PostID;";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId),
                    new("PostID", postId)
                ];
                apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        #endregion
    }
}