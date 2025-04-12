using api.garagecom.Filters;
using api.garagecom.Utils;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

public class Comment
{
    public int CommentID { get; set; }
    public int UserID { get; set; }
    public int ParentID { get; set; }
    public int PostID { get; set; }
    public string CreatedIn { get; set; }
    public string Text { get; set; }
}

public class Post
{
    public int PostID { get; set; }
    public int UserID { get; set; }
    public string Title { get; set; }
    public string CreatedIn { get; set; }
    public int PostCategoryID { get; set; }
    public List<Comment> Comments { get; set; }
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
                    {
                        postCategories.Add(new PostCategory
                        {
                            PostCategoryID = reader["PostCategoryID"] != DBNull.Value
                                ? Convert.ToInt32(reader["PostCategoryID"])
                                : -1,
                            Title = (reader["Title"] != DBNull.Value ? reader["Title"].ToString() : "")!
                        });
                    }
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
                var sql = @"SELECT PostID, UserID, Title, CreatedIn, PostCategoryID
                            FROM Posts
                            WHERE PostCategoryID IN (@PostCategoryID, -10) AND Status = 'Active'
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
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : ""
                        });
                }

                sql = @"SELECT CommentID, Comments.UserID, ParentID, Comments.PostID, Text, Comments.CreatedIn
                            FROM Comments
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID AND Posts.PostCategoryID IN (@PostCategoryID, -10)
                            WHERE Comments.Status = 'Active' AND Posts.Status = 'Active'";
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                    {
                        var postId = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1;
                        var post = posts.FirstOrDefault(p => p.PostID == postId);
                        post?.Comments.Add(new Comment
                        {
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            ParentID = reader["ParentID"] != DBNull.Value ? Convert.ToInt32(reader["ParentID"]) : -1,
                            PostID = postId,
                            Text = (reader["Text"] != DBNull.Value ? reader["Text"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
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

        [HttpGet("GetPost")]
        public ApiResponse GetPost(int postId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var post = new Post();
                var sql = @"SELECT PostID, UserID, Title, CreatedIn, PostCategoryID
                            FROM Posts
                            WHERE PostID = @PostID AND Status = 'Active'
                            ORDER BY CreatedIn DESC";
                MySqlParameter[] parameters =
                [
                    new("PostID", postId)
                ];
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        post = new Post
                        {
                            PostID = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1,
                            PostCategoryID = reader["PostID"] != DBNull.Value
                                ? Convert.ToInt32(reader["PostCategoryID"])
                                : -1,
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            Title = (reader["Title"] != DBNull.Value ? reader["Title"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : ""
                        };
                }

                sql =
                    @"SELECT CommentID, Comments.UserID, ParentID, Comments.PostID, Text, IFNULL(Comments.ModifiedIn, Comments.CreatedIn) AS CreatedIn
                            FROM Comments
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID AND Posts.PostCategoryID IN (@PostCategoryID, -10)
                            WHERE Comments.Status = 'Active' AND Posts.Status = 'Active'";
                using (var reader = DatabaseHelper.ExecuteReader(sql, parameters))
                {
                    while (reader.Read())
                        post.Comments.Add(new Comment
                        {
                            CommentID = reader["CommentID"] != DBNull.Value ? Convert.ToInt32(reader["CommentID"]) : -1,
                            UserID = reader["UserID"] != DBNull.Value ? Convert.ToInt32(reader["UserID"]) : -1,
                            ParentID = reader["ParentID"] != DBNull.Value ? Convert.ToInt32(reader["ParentID"]) : -1,
                            PostID = postId,
                            Text = (reader["Text"] != DBNull.Value ? reader["Text"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
                                : ""
                        });
                }

                apiResponse.Parameters["Post"] = post;
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
        public ApiResponse SetPost(string title, int userId, int postCategoryId, IFormFile file)
        {
            var apiResponse = new ApiResponse();
            try
            {
                var sql = @"INSERT INTO Posts (UserID, Title, PostCategoryID, CreatedIn, Status)
                            VALUES (@UserID, @Title, @PostCategoryID, NOW(), 'Active')";
                MySqlParameter[] parameters =
                [
                    new("UserID", userId),
                    new("Title", title),
                    new("PostCategoryID", postCategoryId)
                ];

                // File upload logic

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
                var sql = @"UPDATE Posts
                            SET Status = 'InActive',
                                ModifiedIn = NOW()
                            WHERE PostID = @PostID";
                MySqlParameter[] parameters =
                [
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

        #region Comments

        [HttpPost("SetComment")]
        public ApiResponse SetComment(int commentId, int userId, int postId, string text, int parentId)
        {
            var apiResponse = new ApiResponse();
            try
            {
                if (commentId != -1)
                {
                    var sql = @"INSERT INTO Comments (UserID, PostID, Text, CreatedIn, Status, ParentID)
                            VALUES (@UserID, @PostID, @Text, NOW(), 'Active', @ParentID)";
                    MySqlParameter[] parameters =
                    [
                        new("UserID", userId),
                        new("PostID", postId),
                        new("Text", text),
                        new("ParentID", parentId)
                    ];
                    apiResponse = DatabaseHelper.ExecuteNonQuery(sql, parameters);
                }
                else
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
                var sql = @"UPDATE Comments
                            SET Status = 'InActive',
                                ModifiedIn = NOW()
                            WHERE CommentID = @CommentID";
                MySqlParameter[] parameters =
                [
                    new("CommentID", commentId)
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
                    @"SELECT CommentID, Comments.UserID, ParentID, Comments.PostID, Text, IFNULL(Comments.ModifiedIn, Comments.CreatedIn) AS CreatedIn
                            FROM Comments
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID AND Posts.PostID = @PostID
                            WHERE Comments.Status = 'Active' AND Posts.Status = 'Active'";
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
                            ParentID = reader["ParentID"] != DBNull.Value ? Convert.ToInt32(reader["ParentID"]) : -1,
                            PostID = postId,
                            Text = (reader["Text"] != DBNull.Value ? reader["Text"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
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
                    @"SELECT CommentID, Comments.UserID, ParentID, Comments.PostID, Text, IFNULL(Comments.ModifiedIn, Comments.CreatedIn) AS CreatedIn, Comments.PostID
                            FROM Comments
                            INNER JOIN Posts ON Comments.PostID = Posts.PostID
                            WHERE Comments.Status = 'Active' AND Posts.Status = 'Active' AND CommentID = @CommentID";
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
                            ParentID = reader["ParentID"] != DBNull.Value ? Convert.ToInt32(reader["ParentID"]) : -1,
                            CommentID = commentId,
                            PostID = reader["PostID"] != DBNull.Value ? Convert.ToInt32(reader["PostID"]) : -1,
                            Text = (reader["Text"] != DBNull.Value ? reader["Text"].ToString() : "")!,
                            CreatedIn = reader["CreatedIn"] != DBNull.Value
                                ? Convert.ToDateTime(reader["CreatedIn"]).ToString("yyyy-MM-dd HH:mm:ss")
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
    }
}