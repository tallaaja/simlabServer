using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARToolServer
{

    class databaseConnection
    {
        //NpgsqlConnection conn;

        const string simlabConnection = "Server=23.100.5.134;Port=5432;User Id=postgres;Password=ilove1;Database=simlab;";

        int userID;
        List<string>[] allocListArray(int howMany)
        {
            List<string>[] lists = new List<string>[howMany];
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i] = new List<string>();
            }
            return lists;
        }

        string[][] listsToArray(List<string>[] lists)
        {
            string[][] res = new string[lists.Length][];
            for(int i = 0; i < lists.Length; i++)
            {
                res[i] = lists[i].ToArray();
            }
            return res;
        }



        void start(string name, string password)
        {
            
            //conn.Open();

            //login(name,password);
        }

        //returns the users id on success (>0), 0 on wrong password or user, -1 on error 
        public int validateLogin(string name, string password)
        {
            using (var conn = new NpgsqlConnection("Server=23.100.5.134;Port=5432;User Id=postgres;Password=ilove1;Database=simlab;"))
            {
                // Retrieve all rows
                using (var cmd = new NpgsqlCommand("SELECT public.\"user\".id FROM public.\"user\" WHERE" +
                                                   "public.\"user\".name like \"" + name + ";", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine(reader.GetString(0));
                        return reader.GetInt32(0);
                    }
                    return 0;
                }
            }
            return -1;
        }


        //returns 3 lists of strings 
        //first contining the names, second  description and last one contains the IDs
        public string[][] getListOfContentPackagesCreatedBy(string creatorName)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(simlabConnection))
            {
                // Retrieve all rows
                using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "SELECT " +
                    "public.video_contectpack.name, " +
                    "public.video_contectpack.description, " +
                    "public.video_contectpack.id AS id " +
                    "FROM public.video_contectpack " +
                    "WHERE public.video_contectpack.owner = " + creatorName + ";", conn))
                {
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<string>[] results = allocListArray(3);
                        while (reader.Read())
                        {
                            results[0].Add(reader.GetString(0));
                            results[1].Add(reader.GetString(1));
                            results[2].Add(reader.GetString(2));
                            Console.WriteLine("read content pack: " + reader.GetString(0));
                        }
                        return listsToArray(results);
                    }
                }
                return null;
            }
        }


        //returns 3 lists of strings 
        //first contining the names, second  description and last one contains the IDs
        public string[][] getListOfVideoSeriesInPackage(string packageID)
        {
            

            using (NpgsqlConnection conn = new NpgsqlConnection(simlabConnection))
            {
                // Retrieve all rows
                using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "SELECT"+
                      "public.video_series.name, "+
                      "public.video_series.description, "+
                      "public.video_series.id " +
                    "FROM public.video_series" +
                    "WHERE public.video_series.part_of_package = " + packageID + ";", conn))
                {
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<string>[] results = allocListArray(3);
                        while (reader.Read())
                        {
                            results[0].Add(reader.GetString(0));
                            results[1].Add(reader.GetString(1));
                            results[2].Add(reader.GetString(2));
                            Console.WriteLine("read series in pack: " + reader.GetString(0));
                        }
                        return listsToArray(results);
                    }
                }
                return null;
            }
        }

            //returns 2 lists of strings 
            //first contining the ids, second the list of lesson names
        public string[][] getVideoIDs_andNamesInSerie(string packageID)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(simlabConnection))
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "SELECT " +
                      "public.\"360video\".id, " +
                      "public.\"360video\".lesson_name " +
                    "FROM " +
                      "public.video_in_series" +
                      "INNER JOIN public.\"360video\" ON(public.video_in_series.video = public.\"360video\".id)" +
                    "WHERE public.video_in_series.series = " + packageID + ";", conn))
                {
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        List<string>[] results = allocListArray(2);
                        while (reader.Read())
                        {
                            results[0].Add(reader.GetString(0));
                            results[1].Add(reader.GetString(1));
                            Console.WriteLine("read video pack: " + reader.GetString(1));
                        }

                        return listsToArray(results);

                    }
                }
            }
            return null;
        }


        //main vid in [0], left video in [1] right video in [2]
        public byte[][] getVideoData(string videoID)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(simlabConnection))
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "SELECT " +
                      "public.\"360video\".data, " +
                      "public.\"360video\".sidevideo1, " +
                      "public.\"360video\".sidevideo2, " +
                    "FROM " +
                       "public.\"360video\" " +
                    "WHERE public.\"360video\".id = " + videoID + ";", conn))
                {
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {

                            Console.WriteLine("read video data for : " + videoID);
                            return new byte[][] { (byte[])reader[0], (byte[])reader[1], (byte[])reader[2] };
                        }
                        return null;

                    }
                }
            }
            return null;
        }


        bool registerUser(string userName, string password, string organization)
        {
            
            using (NpgsqlConnection conn = new NpgsqlConnection(simlabConnection))
            {
                // Insert some data
                using (NpgsqlCommand cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "INSERT INTO data (some_field) VALUES (@p)";
                    cmd.Parameters.AddWithValue("p", "Hello world");
                    cmd.ExecuteNonQuery();
                }

                // Retrieve all rows
                using (NpgsqlCommand cmd = new NpgsqlCommand("SELECT public.\"user\".id FROM public.\"user\" WHERE" +
                                                   "public.\"user\".name like \"" + userName + ";", conn))
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    while (reader.Read())
                        Console.WriteLine(reader.GetString(0));
                return true;
            }
            return false;
        }


        //returns the id of the inserted video or 0 on failure, -1 on error
        //TODO!!
        //TODO!!!
        int insertVideo(int ownerID, string video_fileName, string lessonName, string packageName)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(simlabConnection))
            {
                NpgsqlCommand command = new NpgsqlCommand("select * from tablea where column1 = :column1", conn);
                // Now add the parameter to the parameter collection of the command specifying its type.
                command.Parameters.Add(new NpgsqlParameter("column1", NpgsqlDbType.Integer));
                // Now, prepare the statement.
                command.Prepare();
                // Now, add a value to it and later execute the command as usual.
                command.Parameters[0].Value = 4;


            }
            return 0;

        }

        int addVideoToLessonSeries(int video, int lessonID)
        {
            return 1;
        }

        //returns the id of the lesson series created
        int createLessonSeries(string name, string description)
        {
            return 1;
        }


        bool addExtraVideo1(int video, byte[] extraVideo, string videoName)
        {
            return true;
        }

        bool addExtraVideo2(int video,byte[] extraVideo,string videoName)
        {
            return true;
        }

        int fetchVideosInSeries(string seriesName,string creator)
        {
            return 1;
        }

        int fetchVideo(int video)
        {
            return 1;
        }


    }
}

