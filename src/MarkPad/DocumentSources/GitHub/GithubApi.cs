﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using MarkPad.DocumentSources.MetaWeblog.Service;
using MarkPad.Settings.Models;
using Newtonsoft.Json;
using RestSharp;
using MarkPad.Infrastructure;

namespace MarkPad.DocumentSources.GitHub
{
    public class GithubApi : IGithubApi
    {
        public const string ClientId = "ed0cdbf084078f60b8a3";
        private const string ClientSecret = "a0a442815b7530386c90088a98cfd018877624d2";
        public const string RedirectUri = "http://vikingco.de";
        private const string Accesstokenuri = "https://github.com/login/oauth/access_token";
        private const string ApiBaseUrl = "https://api.github.com/";

        public async Task<string> GetToken(string code)
        {
            var c = new HttpClient();
            var data = new Dictionary<string, string>
                           {
                               {"client_id", ClientId},
                               {"client_secret", ClientSecret},
                               {"code", code}
                           };
            var content = new FormUrlEncodedContent(data);
            var request = await c.PostAsync(Accesstokenuri, content);
            var result = await request.Content.ReadAsStringAsync();
            return GetAccessCode(result);
        }

        static string GetAccessCode(string result)
        {
            return HttpUtility.ParseQueryString(result)["access_token"];
        }

        public async Task<BlogInfo[]> FetchBranches(string token, string user, string repositoryName)
        {
            var client = new RestClient(ApiBaseUrl);
            var restRequest = new RestRequest(string.Format("/repos/{0}/{1}/branches", user, repositoryName));
            var result = await client.ExecuteAwaitableAsync<List<GitBranch>>(restRequest);

            return result.Data.Select(r => new BlogInfo { blogid = r.name, blogName = r.name }).ToArray();
        }

        public async Task<Post[]> FetchFiles(string user, string repositoryName, string branch, string token)
        {
            var client = new RestClient(ApiBaseUrl);
            var restRequest = new RestRequest(string.Format("/repos/{0}/{1}/branches/{2}", user, repositoryName, branch));
            var result = await client.ExecuteAwaitableAsync(restRequest);
            var deserializeObject = JsonConvert.DeserializeObject<dynamic>(result.Content);

            var treeUrl = TreeUrl(deserializeObject);

            restRequest = new RestRequest(new Uri(treeUrl, UriKind.Absolute));
            var tree = await client.ExecuteAwaitableAsync<GitTree>(restRequest);

            return ToPosts(tree);
        }

        static Post[] ToPosts(IRestResponse<GitTree> tree)
        {
            return tree.Data.tree
                .Where(i => i.type == "blob")
                .Select(i => new Post
                {
                    postid = i.sha,
                    title = i.path,
                    permalink = i.url
                })
                .ToArray();
        }

        static string TreeUrl(dynamic deserializeObject)
        {
            var commit = deserializeObject.commit;
            var tree = commit.commit.tree;
            var treeUrl = (string)tree.url;
            return treeUrl;
        }

        public async Task<string> FetchFileContents(string token, string username, string repository, string sha)
        {
            var client = new RestClient(ApiBaseUrl);
            var restRequest = new RestRequest(string.Format("/repos/{0}/{1}/git/blobs/{2}", username, repository, sha));
            var result = await client.ExecuteAwaitableAsync(restRequest);

            return GetContent(result);
        }

        public async Task<GitTree> NewTree(string token, string username, string repository, GitTree tree)
        {
            var client = new RestClient(ApiBaseUrl);
            var restRequest = new RestRequest(string.Format("/repos/{0}/{1}/git/trees", username, repository))
            {
                Method = Method.POST,
                RequestFormat = DataFormat.Json
            };
            restRequest.AddBody(tree);
            restRequest.AddParameter("access_token", token, ParameterType.GetOrPost);

            var restResponse = await client.ExecuteAwaitableAsync<GitTree>(restRequest);
            return GetData(restResponse);
        }

        static T GetData<T>(IRestResponse<T> restResponse)
        {
            return restResponse.Data;
        }

        static string GetContent(IRestResponse result)
        {
            var deserializeObject = JsonConvert.DeserializeObject<dynamic>(result.Content);
            if (deserializeObject.encoding == "utf-8")
                return deserializeObject.content;

            var content = deserializeObject.content;
            string encodedData = content;
            return DecodeFrom64(encodedData);
        }

        public static string DecodeFrom64(string encodedData)
        {
            var encodedDataAsBytes = Convert.FromBase64String(encodedData);

            var returnValue = Encoding.UTF8.GetString(encodedDataAsBytes);

            return returnValue;

        }
    }

    public class GitTree
    {
        public GitTree()
        {
            tree = new List<GitFile>();
        }

        public List<GitFile> tree { get; set; }
    }

    public class GitFile
    {
        public int mode { get; set; }
        public string url { get; set; }
        public string path { get; set; }
        public string type { get; set; }
        public string sha { get; set; }
        public string content { get; set; }
    }

    public class GitBranch
    {
        public string name { get; set; }
    }
}