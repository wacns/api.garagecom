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
    public List<Vote> Votes { get; set; }
    public bool AllowComments { get; set; }
}

public class Vote
{
    public int VoteID { get; set; }
    public int UserID { get; set; }
    public int PostID { get; set; }
    public string CreatedIn { get; set; }
    public int Value { get; set; }
    public string ModifiedIn { get; set; }
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

        #region Posts

        [HttpGet("GetPosts")]
        public ApiResponse GetPosts(int[] categoryId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var posts = new List<Post>();
                var sql =
                    @"SELECT PostID, GeneralInformation.UserName, Posts.AllowComments,Posts.UserID, Posts.Title, Posts.Description, Posts.Attachment, Posts.CreatedIn, Posts.PostCategoryID, PostCategories.Title AS CategoryTitle
                            FROM Posts
                            INNER JOIN PostCategories ON PostCategories.PostCategoryID = Posts.PostCategoryID
                                INNER JOIN GeneralInformation ON GeneralInformation.UserID = Posts.UserID
                                
                            INNER JOIN Statuses ON Statuses.StatusID = Posts.StatusID
                            WHERE Posts.PostCategoryID IN (@PostCategoryID, -10) AND Statuses.Status = 'Active'
                            ORDER BY CreatedIn DESC";
                MySqlParameter[] parameters =
                [
                    new("PostCategoryID", string.Join(",", categoryId))
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
                            Comments = [],
                            Votes = []
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

                sql =
                    @"SELECT Votes.VoteID, Votes.UserID, Votes.PostID, Votes.CreatedIn AS CreatedIn, Votes.Value AS VoteValue, Votes.ModifiedIn
                            FROM Votes
                            INNER JOIN Posts ON Votes.PostID = Posts.PostID AND Posts.PostCategoryID IN (@PostCategoryID, -10)
                            INNER JOIN Statuses ON Statuses.StatusID = Posts.StatusID
                            WHERE Statuses.Status = 'Active'";
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                    {
                        var postId = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1;
                        var post = posts.FirstOrDefault(p => p.PostID == postId);
                        post?.Votes.Add(new Vote
                        {
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            PostID = postId,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            ModifiedIn = reader["ModifiedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["ModifiedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            VoteID = reader["VoteID"] != DBNull.Value ? Convert.ToInt32(reader["VoteID"]) : -1,
                            Value = reader["VoteValue"] != DBNull.Value ? Convert.ToInt32(reader["VoteValue"]) : -1
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

        [HttpGet("GetPost")]
        public ApiResponse GetPost(int postId)
        {
            var apiResponse = new ApiResponse();
            var post = new Post();
            try
            {
                var sql =
                    @"SELECT PostID, GeneralInformation.UserID, GeneralInformation.UserName, Posts.AllowComments,Posts.Title, Posts.Description, Posts.Attachment, Posts.CreatedIn, Posts.PostCategoryID, PostCategories.Title AS CategoryTitle
                            FROM Posts
                            INNER JOIN PostCategories ON PostCategories.PostCategoryID = Posts.PostCategoryID
                                INNER JOIN GeneralInformation ON GeneralInformation.UserID = Posts.UserID
                            INNER JOIN Statuses ON Statuses.StatusID = Posts.StatusID
                            WHERE Statuses.Status = 'Active' AND Posts.PostID = @PostID
                            ORDER BY CreatedIn DESC";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    if (reader.Read())
                        post = new Post
                        {
                            PostID = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1,
                            PostCategoryID = reader["PostID"] != DBNull.Value
                                ? Convert.ToInt32(reader["PostCategoryID"])
                                : -1,
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            UserName = (reader["UserName"] != DBNull.Value ? reader["UserName"].ToString() : "")!,
                            Title = (reader["Title"] != DBNull.Value ? reader["Title"].ToString() : "")!,
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
                            Comments = [],
                            Votes = []
                        };
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

                sql =
                    @"SELECT Votes.VoteID, Votes.UserID, Votes.PostID, Votes.CreatedIn AS CreatedIn, Votes.Value AS VoteValue, Votes.ModifiedIn
                            FROM Votes
                            INNER JOIN Posts ON Votes.PostID = Posts.PostID AND Posts.PostCategoryID IN (@PostCategoryID, -10)
                            INNER JOIN Statuses ON Statuses.StatusID = Posts.StatusID
                            WHERE Statuses.Status = 'Active'";
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        post?.Votes.Add(new Vote
                        {
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            PostID = postId,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            ModifiedIn = reader["ModifiedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["ModifiedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            VoteID = reader["VoteID"] != DBNull.Value ? Convert.ToInt32(reader["VoteID"]) : -1,
                            Value = reader["VoteValue"] != DBNull.Value ? Convert.ToInt32(reader["VoteValue"]) : -1
                        });
                }

                apiResponse.Parameters["Post"] = post!;
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

                var apiResponseScalar = DatabaseHelper.ExecuteScalar(sql, parameters);
                if (apiResponseScalar.Succeeded)
                {
                    var postId = Convert.ToInt32(apiResponseScalar.Parameters["Result"]);
                    // if (file != null)
                    // {
                    //     var fileName = $"{userId}_{Guid.NewGuid().ToString()}";
                    //     var succeeded = await S3Helper.UploadAttachmentAsync(file, fileName, "Images/Posts/");
                    //     if (succeeded)
                    //     {
                    //         sql = @"UPDATE Posts
                    //     SET Attachment = @Attachment
                    //     WHERE UserID = @UserID AND PostID = @PostID";
                    //         parameters =
                    //         [
                    //             new MySqlParameter("Attachment", fileName),
                    //             new MySqlParameter("UserID", userId),
                    //             new MySqlParameter("PostID", postId)
                    //         ];
                    //         apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                    //     }
                    // }
                }

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
                    int postUserId = -1;
                    string deviceToken = "";
                    parameters =
                    [
                        new MySqlParameter("PostID", postId)
                    ];
                    ApiResponse apiResponseScalar = DatabaseHelper.ExecuteScalar(sql, parameters);
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
                            Body = text,
                            
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

        [HttpGet("GetVotesByPostId")]
        public ApiResponse GetVotesByPostId(int postId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var votes = new List<Vote>();
                var sql =
                    @"SELECT Votes.VoteID, Votes.UserID, Votes.PostID, Votes.CreatedIn AS CreatedIn, Votes.Value
                            FROM Votes
                            INNER JOIN Posts ON Votes.PostID = Posts.PostID AND Posts.PostID = @PostID
                            INNER JOIN Statuses ON Statuses.StatusID = Posts.StatusID
                            WHERE Statuses.Status = 'Active'";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        votes.Add(new Vote
                        {
                            VoteID = reader["VoteID"] != DBNull.Value ? Convert.ToInt32(reader["VoteID"]) : -1,
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            PostID = postId,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : "",
                            Value = reader["Value"] != DBNull.Value ? Convert.ToInt32(reader["Value"]) : -1
                        });
                }

                apiResponse.Parameters["Votes"] = votes;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpPost("SetVote")]
        public ApiResponse SetVote(int voteId, int postId, int value)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (voteId != -1)
                {
                    var sql = @"INSERT INTO Votes (UserID, PostID, CreatedIn, Value)
                            VALUES (@UserID, @PostID, NOW(), @UpVote)";
                    MySqlParameter[] parameters =
                    [
                        new("UserID", userId),
                        new("PostID", postId),
                        new("UpVote", value)
                    ];
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                }
                else
                {
                    var sql = @"UPDATE Votes
                            SET Value = @Value,
                                ModifiedIn = NOW()
                            WHERE VoteID = @VoteID";
                    MySqlParameter[] parameters =
                    [
                        new("VoteID", voteId),
                        new("Value", value)
                    ];
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                }
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
            Task task = new Task(async void () =>
            {
                bool status = await S3Helper.UploadAttachmentAsync(file, attachmentName, "Images/Posts/");
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
    }
}