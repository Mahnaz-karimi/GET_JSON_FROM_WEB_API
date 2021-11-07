﻿
using Newtonsoft.Json;

using System;
using System.Data;
using System.Net;
using System.Data.SqlClient;
using System.Timers;
using System.Configuration;

namespace KmdWeb
{
    public class Program

    {
        public static Timer newTimer = new Timer();
        public static int interval_time_for_save = 3; // minut time for check json-data 

        public static dynamic fetchData() // get json data 
        {
            dynamic json;
            using (WebClient wc = new WebClient())
            {
                json = JsonConvert.DeserializeObject(wc.DownloadString("http://127.0.0.1:8000/")); 
            }
            return json;
        }
       

        public static DateTime getLastDateTimeFromDB(string connString)
        {
            SqlCommand cmd = new SqlCommand();
            DateTime dtLine = DateTime.MinValue;

            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                SqlCommand myCommand = new SqlCommand("SELECT TOP (1) [UpdatedAt] FROM [KMD].[dbo].[ValutaKurser] order by UpdatedAt Desc; ", conn);
                SqlDataReader myReader = myCommand.ExecuteReader();
                while (myReader.Read())
                {
                    dtLine = (DateTime)myReader["UpdatedAt"];
                }
            }
            return dtLine;
        }

        public static void print_Json_From_URL(dynamic json)
        {
            DateTime updatedAt = json.updatedAt.Value;
            foreach (var item in json.valutaKurser) // print json-data 
            {
                Console.WriteLine("\n DATA FROM JSON: Rate:  " + string.Format("{0:0.0000000000000000}", (item.rate.Value) +
                    "   fromCurrency: " + item.fromCurrency.Value + "   toCurrency: " + item.toCurrency.Value));
            }

            Console.WriteLine("updatedAt:  " + (updatedAt.ToString("yyyy-MM-dd HH:mm:ss.fffffff")));
        }

        public static void insertJsonDataInSQl(dynamic json, string connString)
        {
            SqlCommand cmd = new SqlCommand();
            using (SqlConnection conn = new SqlConnection(connString)) // save json-data to sql database
                {
                    conn.Open();
                    cmd.Connection = conn;
                    foreach (var item in json.valutaKurser)
                    {
                        Console.WriteLine("INSERTING DATA IN SQL..... Rate is: " + Convert.ToString(item.rate.Value));
                        cmd.CommandText = "INSERT INTO ValutaKurser (FromCurrency, ToCurrency, Rate, UpdatedAt) ";
                        cmd.CommandText += "Values ('" + item.fromCurrency.Value + "', '" + item.toCurrency.Value + "', CAST(" + Convert.ToString(item.rate.Value).Replace(',', '.') + " AS NUMERIC(25,15))," +
                                            "convert(datetime2,'" + json.updatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff") + "'))";
                        cmd.ExecuteNonQuery();
                    }
                }
        }


        public static int getMinuttDifferenceTimeSpam() // diffrence between json and sql-database
        {
            dynamic json = fetchData();
            DateTime updatedAt = json.updatedAt.Value; // get datetime from json
            DateTime lastDateTime = getLastDateTimeFromDB(ConfigurationManager.AppSettings["connectionString"]); // get last datetime from database ??
            TimeSpan ts = updatedAt - lastDateTime; // calculate difrenece time between last data from database and datetime from json...
            int diff = Convert.ToInt32(ts.TotalMinutes);
            Console.WriteLine("\n Difference between valutakurser website and our sql databases is: " + diff + "\n");
            Console.WriteLine("\n lastDateTime is: " + lastDateTime.ToString() + "\n");
            return diff;
        }

        public static int newTimerTime(int MinuttDifferenceTimeSpam)
        {
            int newIntervalInt;
            if (MinuttDifferenceTimeSpam <= interval_time_for_save) // difference is less than timer, we can calculate a new timer-time that will be under 'interval_time_for_save'
            {
                newIntervalInt = ((interval_time_for_save - MinuttDifferenceTimeSpam) * 60 * 1000) + 1000; //  + 1000 = 1 sec is for because interval shoudn't be zero! 
                Console.WriteLine("\n New timer time : " + newIntervalInt / 60 / 1000);
                Console.WriteLine("\n New timer and date time now: " + DateTime.Now);
                return newIntervalInt; // return a new time for timer
            }
            else 
            {                
                DateTime lastDateTime = getLastDateTimeFromDB( ConfigurationManager.AppSettings["connectionString"]); // get last datetime from database 
                TimeSpan ts = DateTime.Now - lastDateTime; // calculate difrenece time between last data from database and datetime from json...
                int diff = Convert.ToInt32(ts.TotalMinutes);
                Console.WriteLine("\n Díffrence between sql last updated and now is: " + diff + "\n");

                // newTimer.Close();
                return 1000;
            }

             // difference is bigger than timer-time, new-timer will be 1 sec for save data in sql
        }

        public static void update_sql_TimerEvent(object source, ElapsedEventArgs e)
        {
            dynamic json = fetchData();
            int minDifference = getMinuttDifferenceTimeSpam(); // difference between json and sql-database

            if ( minDifference > 0) // If diffrence between json and sql is bigger then 0, that means, there should be some diffrence, Therefore data will saved 
            {
                insertJsonDataInSQl(json, ConfigurationManager.AppSettings["connectionString"]);
                Console.WriteLine("\n the time posted on valutakurser website is: " + json.updatedAt.Value);
                Console.WriteLine("\n SQL database is updated. Date time now: " + DateTime.Now);
                int intervalint = newTimerTime(getMinuttDifferenceTimeSpam()); // after saving data, we call newtimer
                newTimer.Interval = intervalint; // starter eventes with new interval
                Console.WriteLine("\n New interval is: " + intervalint / 60 / 1000);
            }
           
        }

        public static void Main(string[] args)
        {
            dynamic json = fetchData(); // Get data from json file 
            print_Json_From_URL(json); // print json file            


            // Timer for events
            newTimer.Elapsed += update_sql_TimerEvent;
            int intervalint = newTimerTime(getMinuttDifferenceTimeSpam()); // timerm will be 1 secend or less than interval_time_for_save
            newTimer.Interval = intervalint; // starter eventes with new interval
            newTimer.Start();
            while (Console.Read() != 'q') {
            };
        }
    }
}
