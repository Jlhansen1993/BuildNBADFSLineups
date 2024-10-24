using BuildNBADFSLineups.BOL;
using BuildNBADFSLineups.Utilities;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Data;
using System.Net;

// Define empty projections.
NBAProjectionList projections = new NBAProjectionList();
NBAEventList events = new NBAEventList();
var builtLineups = new Dictionary<NBAProjectionList, double>();

#region Get Events CSV.
// Get file path for events file.
Console.WriteLine("Get events...");

// Read the file.
string[] lineData = File.ReadAllLines(@$"C:\Users\Justin\Desktop\DFSData\NBA\events_{DateTime.Now.Date.ToString("MMddyyyy")}.csv");

// Loop through each line.
for (int i = 1; i < lineData.Length; i++)
{
    // Get line columns.
    string[] lineColumns = lineData[i].Split(",");

    // Check if the event id is missing.
    if (lineColumns[0].Trim('\"') == "")
    {
        // Break out of loop
        break;
    }

    // Add events.
    events.Add(new NBAEvent()
    {
        EntryId = lineColumns[0].Trim('\"'),
        ContestId = lineColumns[1].Trim('\"'),
        ContestName = lineColumns[2].Trim('\"'),
    });

}
#endregion

#region Get player list from FanDuel.
// Get file path for events file.
Console.WriteLine("\nGet eligible player list...");

// Read the file.
lineData = File.ReadAllLines(@$"C:\Users\Justin\Desktop\DFSData\NBA\players_{DateTime.Now.Date.ToString("MMddyyyy")}.csv");

// Loop through each line.
for (int i = 1; i < lineData.Length; i++)
{
    // Get line columns.
    string[] lineColumns = lineData[i].Split(",");

    // Make sure player isnt ruled out.
    if (lineColumns[11] != "O")
    {
        // Add player to projections.
        projections.Add(new NBAProjection()
        {
            Id = lineColumns[0],
            Position = lineColumns[1],
            Name = lineColumns[3],
            FantasyPointsPerGame = Math.Round(double.TryParse(lineColumns[5], out double avg) ? avg : 0, 2),
            Salary = Convert.ToInt32(lineColumns[7]),
            Team = lineColumns[9]
        });
    }
}
#endregion

#region Get DFF Projections.
Console.WriteLine($"\nGet DFF Projections...");

// Set up ChromeDriver options
var options = new ChromeOptions();
options.AddArgument("start-maximized");
options.AddArgument("--no-sandbox"); // This option can help avoid elevation issues
options.AddArgument("--disable-gpu"); // Optional: Disable GPU for compatibility
options.AddArgument("--disable-dev-shm-usage"); // Recommended to avoid issues with shared memory

// Set up the ChromeDriverService
var chromeDriverService = ChromeDriverService.CreateDefaultService(@"C:\ChromeDriver\chromedriver.exe");

int maxRetries = 3;
int retryCount = 0;

// Define empty html content variable.
string htmlData = "";

while (retryCount < maxRetries)
{
    try
    {
        // Initialize the ChromeDriver with the configured service and options
        using (IWebDriver driver = new ChromeDriver(chromeDriverService, options))
        {
            // Navigate to a website
            driver.Navigate().GoToUrl("https://www.dailyfantasyfuel.com/nba/projections/fanduel");

            // Wait 1 seconds.
            Thread.Sleep(1000);

            // Define original page content.
            string originalPageContent = driver.PageSource;

            // Find slate dropdown.
            IWebElement slateDropdown = driver.FindElement(By.XPath("/html/body/div[2]/div[1]/div[1]/div[2]/div[2]/div/div[1]/div[1]/div[2]/div[2]"));

            // Select the dropdown.
            slateDropdown.Click();

            // Wait 1 seconds.
            Thread.Sleep(1000);

            // Select slates.
            var slates = driver.FindElements(By.XPath("/html/body/div[2]/div[1]/div[1]/div[2]/div[2]/div/dialog/div/div[3]/div/a"));

            // Find the main slate.
            IWebElement mainSlate = slates.Where(s => s.Text.Contains("Main")).First();

            // Select the main slate.
            mainSlate.Click();

            // Wait until page content changes.
            while (driver.PageSource == originalPageContent) { }

            // Get player rows.
            var rows = driver.FindElements(By.XPath("/html/body/div[2]/div[1]/div[2]/div/div/table/tbody/tr[not(contains(@class, 'hidden'))]"));

            // Loop through each row.
            foreach ( var row in rows )
            {
                // Find the player name.
                var name = row.GetAttribute("data-name");

                name = DataCleanup.FixNames(name);

                // Check if we have a matching name.
                if (projections.Any(projection => projection.Name.ToLower().Contains(name.ToLower())))
                {
                    // Get the matching projection.
                    var matchingProjection = projections.Where(projection => projection.Name.ToLower().Contains(name.ToLower())).FirstOrDefault();

                    // Set the Id.
                    matchingProjection.DFFId = row.GetAttribute("data-player_id");

                    // Set the projection.
                    matchingProjection.DFFFProjectedFantasyPoints = Convert.ToDouble(row.GetAttribute("data-ppg_proj"));
                }
                else if (!name.Contains(" IR"))
                {
                    // Print the name that had no matches.
                    Console.WriteLine(name);

                }
            }
        }

        // Break out of loop.
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error occurred: {ex.Message}");
        retryCount++;
    }
}
#endregion

#region Get NF Projections.
Console.WriteLine($"\nGet NF Projections...");

// Define temp web client to start scraping.
using (WebClient webClient = new WebClient())
{
    // Get html data from site.
    htmlData = webClient.DownloadString("https://www.numberfire.com/nba/daily-fantasy/daily-hockey-projections");

    // Define html document and fill it with data.
    HtmlAgilityPack.HtmlDocument htmlDocument = new HtmlAgilityPack.HtmlDocument();
    htmlDocument.LoadHtml(htmlData);

    // Define xpath for scraping.
    string xpath = "/html/body/main/div[2]/div[2]/section/div[4]/div[2]/table/tbody/tr";
    string nameXPath = ".//*[@class='full']";

    // Select nodes based on xpath variable.
    HtmlNodeCollection dataRows = htmlDocument.DocumentNode.SelectNodes(xpath);

    // Process all rows at once using LINQ.
    foreach (var row in dataRows)
    {
        HtmlNodeCollection tableData = row.SelectNodes("td");

        // Preload required columns
        var td0 = tableData[0];
        var td1 = tableData[1];

        // Get name.
        string name = td0.SelectSingleNode(nameXPath)?.InnerText.Trim().Replace("\n", "");

        // Fix names.
        name = DataCleanup.FixNames(name);

        // Get DFS points.
        double points = Convert.ToDouble(td1.InnerText);

        // Check if we have a matching player.
        if (projections.Any(p => p.Name.ToLower() == name.ToLower()))
        {

            // Add points to projection.
            projections.Where(p => p.Name.ToLower() == name.ToLower()).First().NFProjectedFantasyPoints = points;
        }
        else
        {
            if (points > 0)
            {
                Console.WriteLine(name);
            }
        }
    }
}
#endregion

#region Get RW Projections.
Console.WriteLine($"\nGet RW Projections...");

maxRetries = 3;
retryCount = 0;

while (retryCount < maxRetries)
{
    try
    {
        // Initialize the ChromeDriver with the configured service and options
        using (IWebDriver driver = new ChromeDriver(chromeDriverService, options))
        {
            // Navigate to a website
            driver.Navigate().GoToUrl("https://www.rotowire.com/daily/nba/optimizer.php?site=FanDuel");

            // Define original page content.
            string originalPageContent = driver.PageSource;

            // Wait 5 seconds.
            Thread.Sleep(2000);

            // Wait until page content changes.
            while (driver.PageSource == originalPageContent) { }

            // Find table.
            IWebElement dataTable = driver.FindElement(By.Id("player-pool-table"));

            // Scroll the table into view
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", dataTable);

            // Find scrollabl table.
            IWebElement scrollableDiv = driver.FindElement(By.CssSelector("#root > div > div.px-3.md\\:px-4.lg\\:px-6.pb-6 > div:nth-child(4) > div.overflow-y-scroll.h-\\[80vh\\].bg-white.border.border-slate-200"));

            // Wait for a moment to allow for any potential new content loading
            Thread.Sleep(500);

            // Hold last row.
            IWebElement lastRow = null;

            // Hold rows found.
            List<IWebElement> rowData = new List<IWebElement>(); // List to store the rows

            while (true)
            {
                // Get set of rows.
                List<IWebElement> rows = dataTable.FindElements(By.XPath("tbody/tr")).ToList();

                // Remove empty row.
                rows.RemoveAll(r => r.Text == "");

                // Verify rows dont already exist in rowData.
                if (rows.All(row => rowData.Contains(row)))
                {
                    break;
                }

                // Loop through each row.
                foreach (var row in rows)
                {
                    // Verify we dont have already have the row recorded.
                    if (!rowData.Contains(row))
                    {
                        // Get td elements.
                        var tdElements = row.FindElements(By.XPath("td"));

                        // Get data.
                        string name = tdElements[0].Text.Replace("DAY", "").Replace("GTD", "").Trim().Replace("''", "'");
                        double pts = Convert.ToDouble(tdElements[10].FindElement(By.XPath("div/input")).GetAttribute("value"));

                        // If player is out, skip them.
                        if (name.Contains("OUT"))
                        {
                            continue;
                        }

                        // Fix names.
                        name = DataCleanup.FixNames(name);

                        // See if we can find matching player.
                        if (projections.Any(p => p.Name.ToLower().Contains(name.ToLower())))
                        {
                            projections.Where(p => p.Name.ToLower().Contains(name.ToLower())).First().RWProjectedFantasyPoints = pts;
                        }
                        else
                        {
                            if (pts > 0 && !name.Contains("\r\nIR"))
                            {
                                Console.WriteLine(name);
                            }
                        }
                    }
                }

                // Define last row.
                lastRow = rows[rows.Count - 2];

                if (rows.Count <= 25)
                {
                    break;
                }

                // Scroll to last name.
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", lastRow);
            }
        }

        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error occurred: {ex.Message}");
        retryCount++;
    }
}
#endregion

#region Average Projections.
foreach (NBAProjection projection in projections)
{
    // Create array to hold all scores.
    double[] projectedPts = new double[] { projection.DFFFProjectedFantasyPoints, projection.NFProjectedFantasyPoints, projection.RWProjectedFantasyPoints };

    // Filter out the zero values and calculate the average of non-zero items
    var nonZeroScores = projectedPts.Where(score => score > 0).ToArray();

    // Get the average.
    if (nonZeroScores.Length > 0)
    {
        // Define weights for past performance and today's projected points
        double pastPerformanceWeight = 0.7;
        double projectedPointsWeight = 0.3;

        // Calculate the weighted average
        if (projection.FantasyPointsPerGame > 0)
        {
            // Weighted average of past performance and today's projected points
            projection.FinalFantasyPoints = Math.Round(
                (projection.FantasyPointsPerGame * pastPerformanceWeight) + (nonZeroScores.Average() * projectedPointsWeight),
                2);
        }
        else
        {
            // If no FPPG, just use the average of recent non-zero scores
            projection.FinalFantasyPoints = Math.Round(nonZeroScores.Average(), 2);
        }
    }
    else
    {
        // If no recent scores, just use today's projected points
        projection.FinalFantasyPoints = 0;
    }
}
#endregion

#region Open up DFS website and update projections.
Console.WriteLine($"\nOpen chrome and Daily Fantasy Fuel.");

// Initialize the ChromeDriver with the configured service and options
using (IWebDriver driver = new ChromeDriver(chromeDriverService, options))
{
    // Navigate to a website
    driver.Navigate().GoToUrl("https://www.dailyfantasyfuel.com/nba?platform=fd");

    try
    {
        // Wait 1 seconds.
        Thread.Sleep(1000);

        // Find slate dropdown.
        IWebElement slateDropdown = driver.FindElement(By.XPath("/html/body/div[1]/section[1]/div[3]/div[1]/div/div[2]/div/div[2]/div[1]/div[1]/div[1]/div"));

        // Select the dropdown.
        slateDropdown.Click();

        // Wait 1 seconds.
        Thread.Sleep(1000);

        // Select slates.
        var slates = driver.FindElements(By.XPath("/html/body/div[1]/section[1]/div[3]/div[1]/div/div[2]/div/div[2]/div[1]/div[2]/div/a"));

        // Find the main slate.
        IWebElement mainSlate = slates.Where(s => s.Text.Contains("Main")).First();

        // Select the main slate.
        mainSlate.Click();

        // Select slates.
        /*var stackButton = driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > div.col-12.flex.vertical-flex.row-pad-fullpoint > div:nth-child(3) > div"));

        // Click the stack button.
        stackButton.Click();

        // Wait 1 seconds.
        Thread.Sleep(1000);

        // Get element.
        var minimumDifferencesButton = driver.FindElement(By.XPath("/html/body/div[4]/div/div[2]/div/div[6]/div/div/div/div/div[2]/div[3]"));

        // Scroll the button into view
        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", minimumDifferencesButton);

        // Click the button
        js.ExecuteScript("arguments[0].click();", minimumDifferencesButton);

        // Wait 3 seconds.
        Thread.Sleep(1000);

        driver.FindElement(By.XPath("/html/body/div[4]/div/div[1]/div/div/div/span")).Click();

        // Wait 3 seconds.
        Thread.Sleep(3000);*/

        // Click the show more rows button.
        driver.FindElement(By.CssSelector("#listings > div > li")).Click();

        // Get all rows of players.
        var playerRows = driver.FindElements(By.CssSelector("#listings > li"));

        // Loop through each row.
        for (int i = 3; i < playerRows.Count(); i++)
        {
            // Find the player name.
            var playerId = playerRows[i].GetAttribute("data-player_id");

            // Check if we have a matching name.
            if (projections.Any(projection => projection.DFFId == playerId))
            {
                // Get the matching projection.
                var matchingProjection = projections.Where(projection => projection.DFFId == playerId).FirstOrDefault();

                // Set the projection.
                var points = playerRows[i].FindElement(By.CssSelector("div > div.flex.flex-right.vertical-flex.col-pad-right-3.hidden-xs > div > div.col-width-5 > input"));

                // Use JavaScript to set the value of the input element
                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("arguments[0].value=arguments[1];", points, matchingProjection.FinalFantasyPoints.ToString());
            }
        }
    }
    catch { }

    // Build the lineups.
    try
    {
        // Get the search box.
        var optimizerButton = driver.FindElement(By.CssSelector("#optimize-cta-sidebar > div"));

        // Click the optimizer button.
        optimizerButton.Click();

        // Wait for lineups to build.
        while (optimizerButton.Text != "OPTIMIZE")
        {
        }
    }
    catch { }

    // Get lineups.
    try
    {
        // Get lineups bar.
        var lineupsBar = driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > ul.line-filter-container.border-vlt.border-bottom-1 > li > div > div.flex.scrollnav.rel"));

        // Hold up the process until no lineup is disabled.
        while (lineupsBar.FindElements(By.XPath("./div[position() <= 5][contains(@class, 'disabled')]")).Any())
        {
        }

        // Get lineups.
        var lineups = new IWebElement[] {
            driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > ul.line-filter-container.border-vlt.border-bottom-1 > li > div > div.flex.scrollnav.rel > div:nth-child(1)")),
            driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > ul.line-filter-container.border-vlt.border-bottom-1 > li > div > div.flex.scrollnav.rel > div:nth-child(2)")),
            driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > ul.line-filter-container.border-vlt.border-bottom-1 > li > div > div.flex.scrollnav.rel > div:nth-child(3)")),
            driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > ul.line-filter-container.border-vlt.border-bottom-1 > li > div > div.flex.scrollnav.rel > div:nth-child(4)")),
            driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > ul.line-filter-container.border-vlt.border-bottom-1 > li > div > div.flex.scrollnav.rel > div:nth-child(5)"))
        };

        // Loop through each lineup.
        foreach (var lineup in lineups)
        {
            try
            {
                // Click the lineup.
                lineup.Click();

                // Get players.
                var playersInLineup = driver.FindElements(By.CssSelector("#desktop-sidebar-container > div > div > ul.mypicks-desktop.pad-fullpoint > li"));

                var pg1 = projections.FindAll(p => p.DFFId == playersInLineup[0].GetAttribute("data-player_id")).FirstOrDefault();
                var pg2 = projections.FindAll(p => p.DFFId == playersInLineup[1].GetAttribute("data-player_id")).FirstOrDefault();
                var sg1 = projections.FindAll(p => p.DFFId == playersInLineup[2].GetAttribute("data-player_id")).FirstOrDefault();
                var sg2 = projections.FindAll(p => p.DFFId == playersInLineup[3].GetAttribute("data-player_id")).FirstOrDefault();
                var sf1 = projections.FindAll(p => p.DFFId == playersInLineup[4].GetAttribute("data-player_id")).FirstOrDefault();
                var sf2 = projections.FindAll(p => p.DFFId == playersInLineup[5].GetAttribute("data-player_id")).FirstOrDefault();
                var pf1 = projections.FindAll(p => p.DFFId == playersInLineup[6].GetAttribute("data-player_id")).FirstOrDefault();
                var pf2 = projections.FindAll(p => p.DFFId == playersInLineup[7].GetAttribute("data-player_id")).FirstOrDefault();
                var c = projections.FindAll(p => p.DFFId == playersInLineup[8].GetAttribute("data-player_id")).FirstOrDefault();

                // Define temp lineup.
                var tempLineup = new NBAProjectionList { pg1, pg2, sg1, sg2, sf1, sf2, pf1, pf2, c };

                // Get lineup score.
                double score = Convert.ToDouble(driver.FindElement(By.CssSelector("#desktop-sidebar-container > div > div > div.summary-container.vertical-md.row-pad-top-3.row-pad-3 > div > div.col-12.col-md-10.flex > div.col-space-left-3.col-width-5.col-space-right-6.col-space-md-right-5 > span.summary-container-value.text-right > span")).Text);

                // Add lineup to dictionary.
                builtLineups.Add(tempLineup, score);
            }
            catch { }
        }

        // Order lineups by score.
        builtLineups.OrderByDescending(bl => bl.Value).ToList();
    }
    catch { }
}
#endregion

#region Write events to CSV.
// Define file path.
string csvFilePath = @$"C:\Users\Justin\Desktop\DFSData\NBA\event_lineups_{DateTime.Now.ToString("MMddyyy")}.csv";

// Check if file exists, delete it.
if (File.Exists(csvFilePath))
{
    File.Delete(csvFilePath);
}

// Create CSV with data.
using (var csvWriter = new StreamWriter(csvFilePath))
{
    // Define headers.
    csvWriter.WriteLine("entry_id,contest_id,contest_name,PG,PG,SG,SG,SF,SF,PF,PF,C");

    // Group events by contest id.
    var contests = events.GroupBy(e => e.ContestId);

    // Loop through each contest.
    foreach (var contest in contests)
    {
        // Get list of events.
        var eventList = contest.ToList(); // Convert to list to use index

        // Loop through each entry for contest.
        for (int i = 0; i < eventList.Count; i++)
        {
            // Get matching lineup.
            var lineups = builtLineups.ToList();

            // Add data to event.
            eventList[i].PG1 = lineups[i].Key[0].Id;
            eventList[i].PG2 = lineups[i].Key[1].Id;
            eventList[i].SG1 = lineups[i].Key[2].Id;
            eventList[i].SG2 = lineups[i].Key[3].Id;
            eventList[i].SF1 = lineups[i].Key[4].Id;
            eventList[i].SF2 = lineups[i].Key[5].Id;
            eventList[i].PF1 = lineups[i].Key[6].Id;
            eventList[i].PF2 = lineups[i].Key[7].Id;
            eventList[i].C = lineups[i].Key[8].Id;
        }
    }

    // Loop trhough each player.
    foreach (var item in events)
    {
        csvWriter.WriteLine($"{item.EntryId},{item.ContestId},{item.ContestName},{item.PG1},{item.PG2},{item.SG1},{item.SG2},{item.SF1},{item.SF2},{item.PF1},{item.PF2},{item.C}");
    }
}
#endregion
