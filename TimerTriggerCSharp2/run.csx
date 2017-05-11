#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.SendGrid"
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;
using System; 
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

static HttpClient client = null;
public static void Run(TimerInfo myTimer, TraceWriter log)
{
    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
    string meetupBaseUrl = "https://api.meetup.com/self/calendar";
    client = new HttpClient();
    client.BaseAddress = new Uri(meetupBaseUrl);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    string apiCommand ="";   
    string numberOfEvents="30";
    string sigInfo = Environment.GetEnvironmentVariable("siginfo");
    apiCommand = "?photo-host=public&page=" + numberOfEvents + sigInfo;
    //log.Info(apiCommand);
    //log.Info("1");
    Task<string> product = GetProductAsync(log, apiCommand);
    //log.Info("2");
       
}
static async Task<string> GetProductAsync(TraceWriter log, string path )
{
	int countSent = 0;
    string product = null;
    log.Info("1.1");
    Newtonsoft.Json.Linq.JArray temp = null;
    
    HttpResponseMessage response = null;
	try
	{
		response = await client.GetAsync(path);
	}
	catch (Exception ex2)
	{
		log.Info(ex2.ToString());
	}
	log.Info(response.StatusCode.ToString());
	
    if (response.IsSuccessStatusCode)
    {
        log.Info(response.StatusCode.ToString());
        product = response.Content.ReadAsStringAsync().Result;
        //log.Info(product.ToString());
        temp = Newtonsoft.Json.Linq.JArray.Parse(product);

        
        foreach(var item in temp.Children())
        {
		var eventProperties = item.Children<Newtonsoft.Json.Linq.JProperty>();
			
            //Get the event date time and figure out if we want to post based on the number of days until event
		string thisEventTimeFull = eventProperties.FirstOrDefault(x => x.Name == "time").Value.ToString();
			int thisEventDateTimeUTC = int.Parse(thisEventTimeFull.Substring(0,10)); //strip off milliseconds
			var date = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
		    TimeZoneInfo cstZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
			DateTime thisEventDateTime = date.AddSeconds(thisEventDateTimeUTC);
            thisEventDateTime = TimeZoneInfo.ConvertTimeFromUtc(thisEventDateTime, cstZone);
		
			string thisEventDate = thisEventDateTime.ToString("MMMM d");
            int diff = (thisEventDateTime - DateTime.Now.Date).Days;
            if ( (diff == 2) || (diff == 5) || (diff == 10) || (diff == 20))
            {
                //Yes, we will post if the event begins in 2, 5, 10, or 20 days
                log.Info(thisEventDate);
            }
            else
            {
                //log.Info(thisEventDateTime.ToString());
                continue; //Don't want to post
            }
            //future - only post those that don't have "fee"?
            
			var thisEventNameProperty = eventProperties.FirstOrDefault(x => x.Name == "name");
			var thisEventName = thisEventNameProperty.Value; ////This is a JValue type
			//log.Info(thisEventName.ToString());
			var eventLinkURL = eventProperties.FirstOrDefault(x => x.Name == "link").Value;
			//log.Info(eventLinkURL.ToString());

			var eventGroupInfo = eventProperties.FirstOrDefault(x => x.Name == "group").Value;
			var eventGroupProperties = eventGroupInfo.Children<Newtonsoft.Json.Linq.JProperty>();
			string eventGroupName = eventGroupProperties.FirstOrDefault(x => x.Name == "name").Value.ToString();
			log.Info(eventGroupName);
            
            //Need to hand case where the venue may not be provided yet, which is often the case
            string eventVenueName=""; string address=""; string city=""; string state="";
            var eventVenueInfoProp = eventProperties.FirstOrDefault(x => x.Name == "venue");
            if (eventVenueInfoProp != null)
            {
                var eventVenueInfo = eventVenueInfoProp.Value;
                if (eventVenueInfo != null)
                {
                    //log.Info(eventVenueInfo.ToString());
                    var eventVenueProperties = eventVenueInfo.Children<Newtonsoft.Json.Linq.JProperty>();
                    //log.Info(eventVenueProperties.ToString());
                    eventVenueName = eventVenueProperties.FirstOrDefault(x => x.Name == "name").Value.ToString();
                    //log.Info(eventVenueName);
                    address = GetValue(eventVenueProperties, "address_1");
                    city = GetValue(eventVenueProperties,"city");
                    state = GetValue(eventVenueProperties, "state");
                }
            }

			string description = GetValue(eventProperties,"description");
			string howtofind = GetValue(eventProperties, "how_to_find_us");
			string emailSubject = thisEventDate + ": " + eventGroupName + " - " + thisEventName.ToString();
            log.Info(emailSubject);
			string body = thisEventDateTime.ToLongDateString() + " at " + thisEventDateTime.ToLongTimeString() + Environment.NewLine;
			body += Environment.NewLine + eventVenueName + Environment.NewLine;
			body += Environment.NewLine + address + ", " + city + ", " + state;
			body += Environment.NewLine + Environment.NewLine + description + Environment.NewLine;
			body += Environment.NewLine + howtofind;
            body += Environment.NewLine;
            body += Environment.NewLine + "<a href=\"" + eventLinkURL +"\">" + "Click here for event" + "</a>";
  

            Email emailFrom = new Email(Environment.GetEnvironmentVariable("EmailFrom"));
            //log.Info(emailFrom.Address);
			Email emailTo = new Email(Environment.GetEnvironmentVariable("EmailTo"));
            //log.Info(emailTo.Address); 
			var emailMessageContent = new Content("text/html", body); 
			Mail mailMsg = new Mail(emailFrom, emailSubject, emailTo, emailMessageContent);

            try
            {
                string apiKey = Environment.GetEnvironmentVariable("AzureWebJobsSendGridApiKey");
                //log.Info(apiKey.ToString()); 
                dynamic sg = new SendGridAPIClient(apiKey);
                dynamic response2 = await sg.client.mail.send.post(requestBody: mailMsg.Get());
countSent++;		
                log.Info(response2.StatusCode.ToString());
                log.Info(response2.Body.ReadAsStringAsync().Result.ToString()); 
                //log.Info(response2.Headers.ToString());
            }
            catch (Exception ex)
            {
                log.Info(ex.Message);
            }
        }
    }
    else
    {
        log.Info(response.ToString());
    }
    //log.Info("1.5"); 
log.Info("Number of Blog Posts Sent = " + countSent.ToString());
    return null;
}
private static string GetValue(Newtonsoft.Json.Linq.IJEnumerable<Newtonsoft.Json.Linq.JProperty> group, string fieldName)
{
	string value = "";
	var item = group.FirstOrDefault(x => x.Name == fieldName);
	if (item != null)
	{
		var itemValue = item.Value;
		if (itemValue != null) 
			value = itemValue.ToString();
	}
	return value;
}
