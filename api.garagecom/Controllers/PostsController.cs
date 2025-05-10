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
        private const int PageSize = 3;

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

        [HttpPost("SetReport")]
        public ApiResponse SetReport(int itemId, bool isComment = false, bool isPost = false)
        {
            // 1. Get the calling user’s ID
            var userId = HttpContext.Items["UserID"] == null
                ? -1
                : Convert.ToInt32(HttpContext.Items["UserID"]!);

            // 2. Validate flags: exactly one must be true
            if (!(isComment ^ isPost)) // XOR: true if exactly one of them is true
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid request: you must set exactly one of isComment or isPost to true."
                };
            if (userId == -1)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid request: user ID is not set."
                };
            if (itemId <= 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid request: item ID is not set."
                };
            if (isComment && isPost)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid request: you must set exactly one of isComment or isPost to true."
                };

            // 3. Build common parameter list
            var parameters = new[]
            {
                new MySqlParameter("@UserID", userId),
                new MySqlParameter("@ItemID", itemId)
            };

            string sql;
            try
            {
                if (isComment)
                {
                    // 4a. (Optional) Verify comment exists
                    const string checkCommentSql = "SELECT COUNT(*) FROM Comments WHERE CommentID = @ItemID;";
                    var commentCount = Convert.ToInt32(
                        DatabaseHelper.ExecuteScalar(checkCommentSql, parameters).Parameters["Result"].ToString()
                    );
                    if (commentCount == 0)
                        return new ApiResponse
                        {
                            Succeeded = false,
                            Message = $"No comment found with ID = {itemId}."
                        };

                    // 5a. Prepare INSERT for a comment report
                    sql = @"
                INSERT INTO Reports (ReportingUserID, CommentID)
                VALUES (@UserID, @ItemID);
            ";
                }
                else // isPost == true
                {
                    // 4b. (Optional) Verify post exists
                    const string checkPostSql = "SELECT COUNT(*) FROM posts WHERE PostID = @ItemID;";
                    var postCount = Convert.ToInt32(
                        DatabaseHelper.ExecuteScalar(checkPostSql, parameters).Parameters["Result"].ToString()
                    );
                    if (postCount == 0)
                        return new ApiResponse
                        {
                            Succeeded = false,
                            Message = $"No post found with ID = {itemId}."
                        };

                    // 5b. Prepare INSERT for a post report
                    sql = @"
                INSERT INTO Reports (ReportingUserID, PostID)
                VALUES (@UserID, @ItemID);
            ";
                }

                // 6. Execute the insert
                var apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                return apiResponse;
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = ex.Message
                };
            }
        }

        #region Attachments

        [HttpGet("GetUserAvatarByUserId")]
        public async Task<FileResult?> GetUserAvatarByUserId(int userId)
        {
            if (userId <= 0)
                return null;
            var sql = @"SELECT U.Avatar FROM Garagecom.Users U WHERE U.UserID = @UserID";
            MySqlParameter[] parameters =
            {
                new("UserID", userId)
            };
            var avatar = DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString();
            var file = await S3Helper.DownloadAttachmentAsync(avatar, "Images/Avatars/");
            return File(file, "application/octet-stream", avatar);
        }

        [HttpGet("GetPostAttachment")]
        public async Task<FileResult?> GetPostAttachment(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;
            fileName = fileName.SanitizeFileName();
            var file = await S3Helper.DownloadAttachmentAsync(fileName, "Images/Posts/");
            return File(file, "application/octet-stream", fileName);
        }

        [HttpPost("SetPostAttachment")]
        public async Task<ApiResponse> SetPostAttachment(
            [FromForm] int postId,
            [FromForm] IFormFile file)
        {
            if (postId <= 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Invalid request: post ID is not set."
                };
            var sql = @"SELECT PostID FROM Garagecom.Posts P WHERE P.PostID = @PostID";
            MySqlParameter[] parameters =
            {
                new("PostID", postId)
            };
            var postCount = Convert.ToInt32(
                DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
            );
            if (postCount == 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = $"No post found with ID = {postId}."
                };

            // 1) Validate
            if (file.Length == 0)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "No file was uploaded."
                };

            // 2) Upload
            var attachmentName = $"{postId}_{Guid.NewGuid()}";
            bool uploaded;
            try
            {
                uploaded = await S3Helper
                    .UploadAttachmentAsync(file, attachmentName, "Images/Posts/");
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = $"Error uploading to S3: {ex.Message}"
                };
            }

            if (!uploaded)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Failed to upload file to S3."
                };

            // 3) Persist the new key in your Posts table
            sql = @"
        UPDATE Posts
           SET Attachment = @Attachment
         WHERE PostID    = @PostID";
            parameters =
            [
                new MySqlParameter("Attachment", attachmentName),
                new MySqlParameter("PostID", postId)
            ];
            var dbResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);

            if (!dbResponse.Succeeded)
                return new ApiResponse
                {
                    Succeeded = false,
                    Message = "Database update failed."
                };

            // 4) Return
            return new ApiResponse
            {
                Succeeded = true,
                Parameters = { ["AttachmentName"] = attachmentName }
            };
        }

        #endregion

        #region Posts

        [HttpGet("SearchPosts")]
        public ApiResponse SearchPosts(string searchText)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var posts = new List<SearchPostModel>();
                var sql = @"SELECT PostID, Title, Description FROM Posts P";
                var parameters = Array.Empty<MySqlParameter>();

                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        posts.Add(new SearchPostModel
                        {
                            PostID = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1,
                            Title = reader["Title"]?.ToString() ?? "",
                            Description = reader["Description"]?.ToString() ?? ""
                        });
                }

                // Ensure indexing before search
                SearchHelper.IndexPosts(posts);

                var searchResults = SearchHelper.Search(posts, searchText);
                apiResponse.Parameters["Posts"] = searchResults;
                apiResponse.Succeeded = true;
            }
            catch (Exception e)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = e.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetPostByUserID")]
        public ApiResponse GetPostByUserId(int page)
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
         INNER JOIN Users G
                    ON G.UserID = P.UserID
         INNER JOIN Statuses S
                    ON S.StatusID = P.StatusID
         LEFT JOIN VoteData V
                   ON V.PostID = P.PostID
         LEFT JOIN CommentData CD
                   ON CD.PostID = P.PostID
WHERE
    P.UserID = @UserID AND P.StatusID != 3
ORDER BY P.CreatedIn DESC LIMIT @Offset, @PageSize;";
                var offset = ((page == 0 ? 1 : page) - 1) * PageSize;
                MySqlParameter[] parameters =
                [
                    new("UserID", userId),
                    new("Offset", offset),
                    new("PageSize", PageSize)
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
                                : Convert.ToInt32(reader["UserVoteValue"])
                        });
                }

                sql = @"SELECT COUNT(*)
FROM Posts P
WHERE
    P.UserID = @UserID";
                var postsCount =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);

                apiResponse.Parameters["Posts"] = posts;
                apiResponse.Parameters["HasMore"] = PageSize * page < postsCount;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetPostByPostId")]
        public ApiResponse GetPostByPostId(int postId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var posts = new Post();
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
         INNER JOIN Users G
                    ON G.UserID = P.UserID
         INNER JOIN Statuses S
                    ON S.StatusID = P.StatusID
         LEFT JOIN VoteData V
                   ON V.PostID = P.PostID
         LEFT JOIN CommentData CD
                   ON CD.PostID = P.PostID
WHERE
    P.PostID = @PostID AND P.StatusID != 3";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        posts = new Post
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
                                : Convert.ToInt32(reader["UserVoteValue"])
                        };
                }

                apiResponse.Parameters["Post"] = posts;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetPosts")]
        public ApiResponse GetPosts(int[] categoryId, int page)
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
         INNER JOIN Users G
                    ON G.UserID = P.UserID
         INNER JOIN Statuses S
                    ON S.StatusID = P.StatusID
         LEFT JOIN VoteData V
                   ON V.PostID = P.PostID
         LEFT JOIN CommentData CD
                   ON CD.PostID = P.PostID
WHERE
    (@PostCategoryID = ''
        OR FIND_IN_SET(P.PostCategoryID, @PostCategoryID)) AND P.StatusID != 3
ORDER BY P.CreatedIn DESC LIMIT @Offset, @PageSize;";
                var offset = ((page == 0 ? 1 : page) - 1) * PageSize;
                MySqlParameter[] parameters =
                [
                    new("PostCategoryID", string.Join(",", categoryId)),
                    new("UserID", userId),
                    new("Offset", offset),
                    new("PageSize", PageSize)
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
                                : Convert.ToInt32(reader["UserVoteValue"])
                        });
                }

                sql = @"SELECT COUNT(*)
FROM Posts P
WHERE
    (@PostCategoryID = ''
        OR FIND_IN_SET(P.PostCategoryID, @PostCategoryID))";
                var postsCount =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);

                apiResponse.Parameters["Posts"] = posts;
                apiResponse.Parameters["HasMore"] = PageSize * page < postsCount;
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
        public async Task<ApiResponse> SetPost(string title, int postCategoryId, string description, bool allowComments)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                var validate = await AiHelper.ValidateUserText($"Title: {title}\nDescription: {description}");
                if (!validate)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Make sure your post contains appropriate and useful content.";
                    return apiResponse;
                }

                var sql = @"
SELECT StatusID INTO @StatusID
FROM Statuses S
WHERE S.Status = @StatusName;
INSERT INTO Posts (UserID, Title, PostCategoryID, CreatedIn, StatusID, Description, AllowComments)
                            VALUES (@UserID, @Title, @PostCategoryID, NOW(), @StatusID, @Description, @AllowComments);
                            SELECT LAST_INSERT_ID();";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId),
                    new("Title", title),
                    new("PostCategoryID", postCategoryId),
                    new("Description", description),
                    new("StatusName", "Active"),
                    new("AllowComments", allowComments)
                ];
                var scalar = DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString();
                var postId = !string.IsNullOrEmpty(scalar) && !string.IsNullOrWhiteSpace(scalar)
                    ? Convert.ToInt32(scalar)
                    : -1;
                apiResponse.Parameters.Add("PostID", postId);
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpPost("UpdatePost")]
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

        [HttpPost("DeletePost")]
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

        [HttpPost("ClosePost")]
        public ApiResponse ClosePost(int postId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"
UPDATE Posts
                            SET Posts.AllowComments = false,
                                ModifiedIn = NOW()
                            WHERE PostID = @PostID";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId)
                    // new("Status", "InActive")
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
        public async Task<ApiResponse> SetComment(int postId, string text)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                if (postId <= 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Post not found";
                    return apiResponse;
                }

                if (string.IsNullOrEmpty(text))
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Comment is empty";
                    return apiResponse;
                }

                text = text.SanitizeFileName();
                var sql = @"SELECT COUNT(*) FROM Garagecom.Posts P WHERE P.PostID = @PostID";
                MySqlParameter[] parameters =
                {
                    new("PostID", postId)
                };
                var postCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (postCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = $"No post found with ID = {postId}.";
                    return apiResponse;
                }

                var validate = await AiHelper.ValidateUserText(text);
                if (!validate)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Make sure your comment contains appropriate and useful content.";
                    return apiResponse;
                }

                sql = @"
SELECT StatusID INTO @StatusID
FROM Statuses S
WHERE S.Status = @Status;
INSERT INTO Comments (UserID, PostID, Text, CreatedIn, StatusID)
                            VALUES (@UserID, @PostID, @Text, NOW(), @StatusID)";
                parameters =
                [
                    new MySqlParameter("UserID", userId),
                    new MySqlParameter("PostID", postId),
                    new MySqlParameter("Text", text),
                    new MySqlParameter("Status", "Active")
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
                    if (apiResponseScalar.Succeeded) deviceToken = apiResponseScalar.Parameters["Result"].ToString();

                    if (string.IsNullOrEmpty(deviceToken) || string.IsNullOrWhiteSpace(deviceToken)) return;
                    var notification = new NotificationRequest
                    {
                        DeviceToken = deviceToken,
                        Title = "New Comment On Your Post",
                        Body = text
                    };
                    var notificationResponse = NotificationHelper.SendNotification(notification);
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
        public async Task<ApiResponse> UpdateComment(int commentId, string text)
        {
            var apiResponse = new ApiResponse();
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Comment is empty";
                    return apiResponse;
                }

                text = text.SanitizeFileName();
                var sql = @"SELECT COUNT(*) FROM Garagecom.Comments C WHERE C.CommentID = @CommentID";
                MySqlParameter[] parameters =
                {
                    new("CommentID", commentId)
                };
                var commentCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (commentCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = $"No comment found with ID = {commentId}.";
                    return apiResponse;
                }

                var validate = await AiHelper.ValidateUserText(text);
                if (!validate)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Make sure your comment contains appropriate and useful content.";
                    return apiResponse;
                }

                sql = @"UPDATE Comments
                            SET Text = @Text,
                                ModifiedIn = NOW()
                            WHERE CommentID = @CommentID";
                parameters =
                [
                    new MySqlParameter("CommentID", commentId),
                    new MySqlParameter("Text", text)
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
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                if (commentId <= 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Comment not found";
                    return apiResponse;
                }

                var sql = @"SELECT COUNT(*) FROM Garagecom.Comments C WHERE C.CommentID = @CommentID";
                MySqlParameter[] parameters =
                {
                    new("CommentID", commentId)
                };
                var commentCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (commentCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = $"No comment found with ID = {commentId}.";
                    return apiResponse;
                }

                sql = @"
SELECT StatusID INTO @StatusID
FROM Statuses S
WHERE S.Status = @Status;
UPDATE Comments
                            SET StatusID = @StatusID,
                                ModifiedIn = NOW()
                            WHERE CommentID = @CommentID";
                parameters =
                [
                    new MySqlParameter("CommentID", commentId),
                    new MySqlParameter("Status", "InActive")
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
        public ApiResponse GetCommentsByPostId(int postId, int page)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                if (postId <= 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Post not found";
                    return apiResponse;
                }

                var sql = @"SELECT COUNT(*) FROM Garagecom.Posts P WHERE P.PostID = @PostID";
                MySqlParameter[] parameters =
                {
                    new("PostID", postId)
                };
                var postCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (postCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = $"No post found with ID = {postId}.";
                    return apiResponse;
                }

                var comments = new List<Comment>();
                sql =
                    @"SELECT CommentID, Comments.UserID, Users.UserName, Comments.PostID, Text, Comments.CreatedIn AS CreatedIn, Comments.ModifiedIn
                            FROM Comments
                                INNER JOIN Users ON Users.UserID = Comments.UserID
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID AND Posts.PostID = @PostID
                            INNER JOIN Statuses SC ON SC.StatusID = Comments.StatusID
                            INNER JOIN Statuses SP ON SP.StatusID = Posts.StatusID
                            WHERE SC.Status != 'Deleted' AND SP.Status = 'Deleted' ORDER BY Comments.CreatedIn LIMIT @Offset, @PageSize;";
                var offset = ((page == 0 ? 1 : page) - 1) * PageSize;
                parameters =
                [
                    new MySqlParameter("PostID", postId),
                    new MySqlParameter("Offset", offset),
                    new MySqlParameter("PageSize", PageSize)
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

                sql = @"SELECT COUNT(*)
FROM Garagecom.Comments C
WHERE
    C.PostID = @PostID";
                var postsCount =
                    int.Parse(DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString() ??
                              string.Empty);

                apiResponse.Parameters["Comments"] = comments;
                apiResponse.Parameters["HasMore"] = PageSize * page < postsCount;
                apiResponse.Succeeded = true;
            }
            catch (Exception ex)
            {
                apiResponse.Succeeded = false;
                apiResponse.Message = ex.Message;
            }

            return apiResponse;
        }

        [HttpGet("GetCommentByCommentID")]
        public ApiResponse GetCommentByCommentId(int commentId)
        {
            var userId = HttpContext.Items["UserID"] == null ? -1 : Convert.ToInt32(HttpContext.Items["UserID"]!);
            var apiResponse = new ApiResponse();
            try
            {
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                if (commentId <= 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Comment not found";
                    return apiResponse;
                }

                var sql = @"SELECT COUNT(*) FROM Garagecom.Comments C WHERE C.CommentID = @CommentID";
                MySqlParameter[] parameters =
                {
                    new("CommentID", commentId)
                };
                var commentCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (commentCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = $"No comment found with ID = {commentId}.";
                    return apiResponse;
                }

                var comment = new Comment();
                sql =
                    @"SELECT CommentID, Comments.UserID, Users.UserName, Comments.PostID, Text, Comments.CreatedIn AS CreatedIn, Comments.ModifiedIn, Comments.PostID
                            FROM Comments
                            INNER JOIN Users ON Users.UserID = Comments.UserID
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID
                            INNER JOIN Statuses SC ON SC.StatusID = Comments.StatusID
                            INNER JOIN Statuses SP ON SP.StatusID = Posts.StatusID
                            WHERE SC.Status = 'Deleted' AND SP.Status = 'Deleted' AND CommentID = @CommentID";
                parameters =
                [
                    new MySqlParameter("CommentID", commentId)
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
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                if (postId <= 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Post not found";
                    return apiResponse;
                }

                if (value != 1 && value != -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Vote value is invalid";
                    return apiResponse;
                }

                var sql = @"SELECT COUNT(*) FROM Garagecom.Posts P WHERE P.PostID = @PostID";
                MySqlParameter[] parameters =
                {
                    new("PostID", postId)
                };
                var postCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (postCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = $"No post found with ID = {postId}.";
                    return apiResponse;
                }

                sql =
                    @"SELECT COUNT(*) FROM Garagecom.Votes V WHERE V.UserID = @UserID AND V.PostID = @PostID AND V.StatusID = (SELECT S.StatusID FROM Garagecom.Statuses S WHERE S.Status = 'Active')";
                parameters =
                [
                    new MySqlParameter("UserID", userId),
                    new MySqlParameter("PostID", postId)
                ];
                var voteCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (voteCount > 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "You have already voted for the post.";
                    return apiResponse;
                }

                sql = @"SELECT StatusID INTO @StatusID FROM Statuses S WHERE S.Status = 'Active';
                        INSERT INTO Votes (UserID, PostID, CreatedIn, Value, StatusID)
                            VALUES (@UserID, @PostID, NOW(), @UpVote, @StatusID)";
                parameters =
                [
                    new MySqlParameter("UserID", userId),
                    new MySqlParameter("PostID", postId),
                    new MySqlParameter("UpVote", value)
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
                if (userId == -1)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "User not found";
                    return apiResponse;
                }

                if (postId <= 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "Post not found";
                    return apiResponse;
                }

                var sql = @"SELECT COUNT(*) FROM Garagecom.Posts P WHERE P.PostID = @PostID";
                MySqlParameter[] parameters =
                {
                    new("PostID", postId)
                };
                var postCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (postCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = $"No post found with ID = {postId}.";
                    return apiResponse;
                }

                sql =
                    @"SELECT COUNT(*) FROM Garagecom.Votes V WHERE V.UserID = @UserID AND V.PostID = @PostID AND V.StatusID = (SELECT S.StatusID FROM Garagecom.Statuses S WHERE S.Status = 'Active')";
                parameters =
                [
                    new MySqlParameter("UserID", userId),
                    new MySqlParameter("PostID", postId)
                ];
                var voteCount = Convert.ToInt32(
                    DatabaseHelper.ExecuteScalar(sql, parameters).Parameters["Result"].ToString()
                );
                if (voteCount == 0)
                {
                    apiResponse.Succeeded = false;
                    apiResponse.Message = "No vote found for the post.";
                    return apiResponse;
                }

                sql = @"SELECT StatusID INTO @StatusID FROM Statuses S WHERE S.Status = 'InActive';
                        UPDATE Votes
                            SET StatusID = @StatusID,
                                ModifiedIn = NOW()
                            WHERE UserID = @UserID AND PostID = @PostID;";
                parameters =
                [
                    new MySqlParameter("UserID", userId),
                    new MySqlParameter("PostID", postId)
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