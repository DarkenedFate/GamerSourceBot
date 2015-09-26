using NLog;
using Quartz;
using Quartz.Impl;
using RedditSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace GamerSourceBot
{
    public partial class GamerBotService : ServiceBase
    {
        private Logger _logger = LogManager.GetLogger("Service");
        private List<string> feedSources = new List<string>();
        private List<string> keywords = new List<string>();
        private IScheduler _scheduler;

        public GamerBotService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Init();
        }

        protected override void OnStop()
        {
            _scheduler.Shutdown();
        }

        public void Init()
        {
            _logger.Info("Init called");

            _scheduler = StdSchedulerFactory.GetDefaultScheduler();
            _scheduler.Start();

            IJobDetail job = JobBuilder.Create<PostStuff>()
                .WithIdentity("postStuff", "group1")
                .Build();
            job.JobDataMap["service"] = this;

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity("trigger1", "group1")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(10)
                    .RepeatForever())
                .Build();

            _scheduler.ScheduleJob(job, trigger);

            Thread.Sleep(5000);
        }

        public void PostArticles()
        {
            StreamReader txtReader = new StreamReader("feeds.txt");
            feedSources = txtReader.ReadToEnd().Split(',').ToList();
            txtReader.Close();

            txtReader = new StreamReader("keywords.txt");
            keywords = txtReader.ReadToEnd().ToLower().Split(',').ToList();
            txtReader.Close();


            Reddit reddit = new Reddit(Credentials.USERNAME, Credentials.PASSWORD);
            var subreddit = reddit.GetSubreddit("gamersource");
            List<string> existingTitles = new List<string>();
            Dictionary<string, int> dictionary = new Dictionary<string, int>();
            List<Uri> uriFeedList = new List<Uri>();

            foreach (var url in feedSources)
                uriFeedList.Add(new Uri(url));

            foreach (var uri in uriFeedList)
            {
                dictionary[uri.Host.Replace("feeds.", "")] = 0;
            }

            foreach (var post in subreddit.New.Take(25))
            {
                existingTitles.Add(post.Title);
                try
                {
                    dictionary[post.Url.Host]++;
                }
                catch (Exception ex)
                {
                    _logger.Warn("No key found for {0}", post.Url.Host);
                }
            }

            var biggestNumberOfPostsFrom = dictionary.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;


            foreach (var source in feedSources)
            {
                if (source.Contains(biggestNumberOfPostsFrom))
                {
                    _logger.Info("Skipping {0} because too many posts from there", biggestNumberOfPostsFrom);
                    continue;
                }

                var reader = XmlReader.Create(source);
                var feed = SyndicationFeed.Load(reader);

                reader.Close();

                foreach (var article in feed.Items)
                {
                    var keywordsMatchList = keywords.Intersect(article.Title.Text.ToLower().Split(' ')).ToList();

                    bool shouldExit = false;
                    foreach (var title in existingTitles)
                    {
                        if (title.Split(' ').Intersect(article.Title.Text.ToLower().Split(' ')).ToList().Count > 5)
                        {
                            shouldExit = true;
                        }
                    }


                    if (shouldExit)
                        break;

                    if (!existingTitles.Contains(article.Title.Text) && keywordsMatchList.Count > 0)
                    {
                        try
                        {
                            subreddit.SubmitPost(article.Title.Text, article.Links[0].Uri.ToString());
                            _logger.Info("Posted article {0}", article.Title.Text);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn("Could not post article {0}", article.Title.Text);
                        }
                        break;
                    }
                }
            }

            _logger.Info("Finished");
        }
    }

    public class PostStuff : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            GamerBotService service = (GamerBotService)context.JobDetail.JobDataMap.Get("service");
            service.PostArticles();
        }
    }
}
