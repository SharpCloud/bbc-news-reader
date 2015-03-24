using System;
using System.Configuration;
using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using SC.API.ComInterop;
using SC.API.ComInterop.Models;

namespace NewsReader
{
    class Program
    {

        public static void UpdateNewsFeed()
        {
            string newsFeedURL = ConfigurationManager.AppSettings["BBCFeedURL"];
            string sharpCloudUsername = ConfigurationManager.AppSettings["SharpCloudUsername"];
            string sharpCloudPassword = ConfigurationManager.AppSettings["SharpCloudPassword"];
            string sharpCloudStoryID = ConfigurationManager.AppSettings["SharpCloudStoryID"];

            //First, download the RSS feed data from the BBC News website
            XmlReader reader = XmlReader.Create(newsFeedURL);
            SyndicationFeed feed = SyndicationFeed.Load(reader);
            reader.Close();

            //Connect to SharpCloud, and load the story
            var _client = new SharpCloudApi(sharpCloudUsername, sharpCloudPassword);
            Story rm = _client.LoadStory(sharpCloudStoryID);

            //Ensure that the categories exist, if not then create them
            Category worldCat = rm.Category_FindByName("World") ?? rm.Category_AddNew("World");
            Category busCat = rm.Category_FindByName("Business") ?? rm.Category_AddNew("Business");
            Category sportCat = rm.Category_FindByName("Sport") ?? rm.Category_AddNew("Sport");
            Category techCat = rm.Category_FindByName("Technology") ?? rm.Category_AddNew("Technology");
            Category enterCat = rm.Category_FindByName("Entertainment") ?? rm.Category_AddNew("Entertainment");
            Category healthCat = rm.Category_FindByName("Health") ?? rm.Category_AddNew("Health");
            Category otherCat = rm.Category_FindByName("Other") ?? rm.Category_AddNew("Other");

            foreach (SyndicationItem newsItem in feed.Items)
            {
                //Try to find the item in the story using the news item id as the external id
                Item scItem = rm.Item_FindByExternalId(newsItem.Id);
                if (scItem == null)
                {
                    var wc = new WebClient();
                    byte[] imageBytes=  null;
                    if (newsItem.ElementExtensions.Count > 0)
                    {
                        //download the news story image from the web
                        try
                        {
                            string imageURL = newsItem.ElementExtensions[1].GetObject<XElement>().LastAttribute.PreviousAttribute.Value;                    
                            imageBytes = wc.DownloadData(imageURL);
                        }
                        catch (Exception)
                        {

                        }
                    }

                    String newsTitle = newsItem.Title.Text;
                    String summary = newsItem.Summary.Text;

                    //create a new item in SharpCloud, with a name which is the title of the news story
                    scItem = rm.Item_AddNew(newsTitle);

                    //look at the URL of the news story to figure out which category it should be in
                    if (newsItem.Links[0].Uri.ToString().Contains("world"))
                        scItem.Category = worldCat;
                    else if (newsItem.Links[0].Uri.ToString().Contains("business"))
                        scItem.Category = busCat;
                    else if (newsItem.Links[0].Uri.ToString().Contains("sport"))
                        scItem.Category = sportCat;
                    else if (newsItem.Links[0].Uri.ToString().Contains("technology"))
                        scItem.Category = techCat;
                    else if (newsItem.Links[0].Uri.ToString().Contains("entertainment"))
                        scItem.Category = enterCat;
                    else if (newsItem.Links[0].Uri.ToString().Contains("health"))
                        scItem.Category = healthCat;
                    else
                        scItem.Category = otherCat;

                    scItem.Description = summary;
                    scItem.ExternalId = newsItem.Id;
                    scItem.StartDate = newsItem.PublishDate.DateTime;
                    scItem.Resource_AddName("BBC News URL", "Click here to view the news story on the web", newsItem.Links[0].Uri.ToString());

                    if (imageBytes != null)
                    {
                        scItem.ImageId = _client.UploadImageData(imageBytes, "", false);
                    }

                    try
                    {
                        //now look at the web page for the story and extract the main text
                        var getHtmlWeb = new HtmlWeb();
                        var document = getHtmlWeb.Load(newsItem.Links[0].Uri.ToString());
                        if (document != null)
                        {
                            var node = document.DocumentNode.SelectSingleNode("//div[@class='story-body__inner']");
                            if (newsItem.Links[0].Uri.ToString().Contains("sport"))
                            {
                               node = document.DocumentNode.SelectSingleNode("//div[@class='article']");
                            
                            }
                                if (node != null)
                                {
                                    string newsText = "";
                                    foreach (var child in node.ChildNodes)
                                    {
                                        if (child.Name == "p")
                                        {
                                            if (newsText != "")
                                                newsText += "<BR><BR>";
                                            newsText += child.InnerHtml;
                                        }
                                        else if (child.Name == "span")
                                        {
                                            if (newsText != "")
                                                newsText += "<BR><BR>";
                                            newsText += "<b>" + child.InnerHtml + "</b>";
                                        }
                                    }
                                    //create a text panel on the item with the text of the story that we have from the web page
                                    scItem.Panel_Add("News Detail", Panel.PanelType.RichText, newsText);
                                }
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }

            //update the story description with the date/time
            rm.Description = string.Format("Last Updated (UTC): {0:ddd dd MMM yyyy HH:mm}", DateTime.UtcNow);

            //save the story to SharpCloud
            rm.Save();

            //now see if we need to delete any items
            bool deletedAnyItems = false;
            foreach (Item scnewsItem in rm.Items)
            {
                bool FoundItem = false;
                foreach (SyndicationItem newsItem in feed.Items)
                {
                    if (scnewsItem.ExternalId == newsItem.Id)
                    {
                        FoundItem = true;
                        break;
                    }
                }
                if (FoundItem == false)
                {
                    //delete it
                    rm.Item_DeleteById(scnewsItem.Id);
                    deletedAnyItems = true;
                }
            }
            if (deletedAnyItems)
                rm.Save();
            
            
            Environment.Exit(0);

        }

        private static void EnsureCategoriesExist()
        {


        }

        static void Main(string[] args)
        {
            UpdateNewsFeed();
            Console.ReadLine();
        }
    }
}
