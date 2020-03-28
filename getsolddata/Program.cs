using librets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace getsolddata
{
    class Program
    {
        static void Main(string[] args)
        {

            List<string> classTypes = new List<string>();
            classTypes.Add("CondoProperty");
            classTypes.Add("ResidentialProperty");
            //classTypes.Add("CommercialProperty");


            int recordprocessed = 0;
            int recordUpdated = 0;
            int recordInserted = 0;
            RetsSession session = new RetsSession("http://retsau.torontomls.net:6103/rets-treb3pv/server/login");
            session.SetUserAgentAuthType(UserAgentAuthType.USER_AGENT_AUTH_RETS_1_7);
            string username = "";
            //if (isCompareListing)
            //{
            //    username = ConfigurationManager.AppSettings["retsusername_a"];

            //}
            //else
            {
                username = "EV17mur";
            }
            //if (!session.Login(username, ConfigurationManager.AppSettings["retspassword"]))
            if (!session.Login(username, "J24#w16"))
            {
                //SaveErrorLog("Failed to login in rets server", 0, 1, _mapper);
                //return "Failed to login in rets server";
            }
            else
            {
                processing(classTypes, session);

            }

            Console.ReadLine();
        }

        private static void processing(List<string> classTypes, RetsSession session)
        {
            var statuslist = new List<string> { "A"};
            foreach (string status in statuslist)
            {
                RetsVersion version = session.GetDetectedRetsVersion();
                string query = $"(Status = {status})";
                //string query = "(TimestampSql= Sept 18, 2019)";

                SearchRequest searchRequest = session.CreateSearchRequest(
                "Property",
                classTypes.FirstOrDefault(),  // owner type
                query);

                searchRequest.SetStandardNames(true);
                searchRequest.SetQueryType(SearchRequest.QueryType.DMQL);
                searchRequest.SetCountType(SearchRequest.CountType.NO_RECORD_COUNT);
                searchRequest.SetFormatType(SearchRequest.FormatType.COMPACT);
                SearchResultSet results = session.Search(searchRequest);
                IEnumerable columns = results.GetColumns();
                Dictionary<string, string> _dictRETS = new Dictionary<string, string>();
                //if (isCompareListing)
                //{
                //    recordprocessed += CompareListing(results, Ownertype);

                //}
                //else
                {
                    List<Dictionary<string, string>> _dictRETSlist = new List<Dictionary<string, string>>();
                    var imgdownloadlist = new List<PhotoDownlaodObject>();
                    int recordprocessed= 0;
                    while (results.HasNext())
                    {
                        recordprocessed++;
                        _dictRETS = new Dictionary<string, string>();
                        foreach (string column in columns)
                        {
                            try
                            {
                                _dictRETS.Add(column, results.GetString(column));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                Console.WriteLine("______________________________________");
                            }

                        }

                        if (_dictRETS["MLS"] != null && _dictRETS["MLS"] != string.Empty)
                        {
                            var mls = _dictRETS["MLS"];
                            _dictRETSlist.Add(_dictRETS);
                            var data = _dictRETS["TimestampSql"];
                            if (DateTime.Parse(data) >=DateTime.Now.AddMonths(-6))
                            {

                                imgdownloadlist.Add(
                                    new PhotoDownlaodObject()
                                    {
                                        MLSID = _dictRETS["MLS"],
                                        LastUpdated = DateTime.Now
                                    });

                            }
                            if (_dictRETSlist.Count >= 200)
                            {
                                try
                                {

                                    var table = ListtoDataTableConverter.ToDataTable(_dictRETSlist);
                                    Task.Factory.StartNew(() =>
                                    {
                                        BulkInsert(table);
                                    });
                                    
                                    _dictRETSlist.Clear();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                    Console.WriteLine("______________________________________");
                                }
                            }
                        }
                        _dictRETS = null;
                        Console.WriteLine(recordprocessed);

                    }
                    var table1 = ListtoDataTableConverter.ToDataTable(_dictRETSlist);
                    Task.Factory.StartNew(() =>
                    {
                        BulkInsert(table1);
                    });

                    DownloadAllListingPhotos(imgdownloadlist, session);

                    //while (results.HasNext())
                    //{

                    //    foreach (string column in columns)
                    //    {
                    //        _dictRETS.Add(column, results.GetString(column));
                    //    }
                    //    _dictRETSlist.Add(_dictRETS);
                    //    _dictRETS = null;
                    //    _dictRETS = new Dictionary<string, string>();
                    //}
                    //var dt = ListtoDataTableConverter.ToDataTable(_dictRETSlist);

                    //StreamWriter wr = new StreamWriter(@"D:\\BookA.xls");


                    //for (int i = 0; i < dt.Columns.Count; i++)
                    //{
                    //    wr.Write(dt.Columns[i].ToString().ToUpper() + "\t");
                    //}

                    //wr.WriteLine();

                    ////write rows to excel file
                    //for (int i = 0; i < (dt.Rows.Count); i++)
                    //{
                    //    for (int j = 0; j < dt.Columns.Count; j++)
                    //    {
                    //        if (dt.Rows[i][j] != null)
                    //        {
                    //            wr.Write(Convert.ToString(dt.Rows[i][j]) + "\t");
                    //        }
                    //        else
                    //        {
                    //            wr.Write("\t");
                    //        }
                    //    }
                    //    //go to next line
                    //    wr.WriteLine();
                    //}
                    ////close file
                    //wr.Close();
                }
               // break;
            }
        }

        public static void BulkInsert(DataTable dt)
        {

            Console.WriteLine($"comming to save record count {dt.Rows.Count}");
            string strcon = @"data source=WIN-7AMPTHSKNTN\MSSQLSERVER01;initial catalog=RealEstateCurrent;integrated security=True;MultipleActiveResultSets=True;";
            using (SqlConnection sqlConnection = new SqlConnection(strcon))
            {
                if (sqlConnection.State == ConnectionState.Closed)
                {
                    sqlConnection.Open();
                }
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(sqlConnection))
                {
                    try
                    {

                        bulkCopy.DestinationTableName = "vobdata";
                        bulkCopy.WriteToServer(dt);
                        dt.Dispose();

                    }
                    catch (Exception ex)
                    {


                    }
                }
            }

        }

        public static void DownloadAllListingPhotos(List<PhotoDownlaodObject> _PhotoDownlaodObject, RetsSession Session)
        {
            Task.Factory.StartNew(() =>
            {
                Console.WriteLine(_PhotoDownlaodObject.Count());
                _PhotoDownlaodObject.ForEach(objProp =>
                {
                    int icount = 1;
                    Console.WriteLine("image count" + icount);
                    try
                    {
                        string photoFilePath = $@"D:\\testaccount\PropertyPhoto\{objProp.MLSID}";
                        bool exists = System.IO.Directory.Exists((photoFilePath));
                        if (!exists)
                            System.IO.Directory.CreateDirectory((photoFilePath));
                        //taken from app.config in  connection example
                        string CurrentMLS = objProp.MLSID;
                        int intCurrentPhotoNo = 1;

                        //loop through objects until obj is null. It does not error out if the objectID does not exist.
                        ObjectDescriptor obj;
                        List<PropertyPhoto> objPhotos = new List<PropertyPhoto>();
                        int objectId = 0;
                        do
                        {
                            using (librets.GetObjectRequest request = new GetObjectRequest("Property", "Photo"))
                            {
                                request.AddObject(CurrentMLS, intCurrentPhotoNo);
                                string strFilename = string.Empty;
                                // Create the file name.
                                
                                strFilename = CurrentMLS + "_" + intCurrentPhotoNo + ".jpg";
                                string fullPath = (Path.Combine(photoFilePath, strFilename));
                                if (!File.Exists(fullPath))
                                {
                                    GetObjectResponse response = Session.GetObject(request);
                                    obj = response.NextObject();
                                    objectId = obj.GetObjectId();
                                    if (objectId > 0)
                                    {
                                        // get the bytes of the downloaded image
                                        byte[] imageBytes = obj.GetDataAsBytes();
                                        // Write the file.
                                        File.WriteAllBytes(fullPath, imageBytes);
                                        //objPhotos.Add(_mapper.Map<PropertyPhotoObject, Data.DB.PropertyPhoto>(new PropertyPhotoObject
                                        //{
                                        //    LastUpdated = Convert.ToDateTime(objProp.LastUpdated),
                                        //    PhotoId = intCurrentPhotoNo,
                                        //    PhotoPath = strFilename,
                                        //    MLSID = CurrentMLS
                                        //}));
                                        // Increment photo number.
                                        intCurrentPhotoNo++;

                                    }
                                }

                            }

                        }
                        while (objectId > 0);
                        //PropertyPhotoService _photoService = new PropertyPhotoService();
                        //_photoService.SavePropertyDetails(objPhotos);
                        
                    }

                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine("______________________________________");
                    }

                    icount++;
                });
                Console.WriteLine("Image downloading Completed");
            });

        }

    }
    public class ListtoDataTableConverter

    {

        public static DataTable ToDataTable(List<Dictionary<string, string>> items)

        {

            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("ID");
            foreach (string prop in items.FirstOrDefault().Keys)
            {
                dataTable.Columns.Add(prop);

            }

            foreach (Dictionary<string, string> item in items)

            {

                var values = new object[item.Count + 1];
                int i = 0;
                values[i] = 0;
                i++;
                foreach (KeyValuePair<string, string> pair in item)
                {
                    values[i] = pair.Value;
                    i++;
                }

                dataTable.Rows.Add(values);

            }

            //put a breakpoint here and check datatable

            return dataTable;

        }

    }

    public class PhotoDownlaodObject
    {
        public string MLSID { get; internal set; }
        public DateTime? LastUpdated { get; internal set; }
    }
    public partial class PropertyPhoto
    {

        public int Id { get; set; }

        public Nullable<int> PhotoId { get; set; }

        public string PhotoPath { get; set; }

        public Nullable<System.DateTime> LastUpdated { get; set; }

        public string MLSID { get; set; }

    }
    public class PropertyPhotoObject
    {
        public int Id { get; set; }
        public Nullable<int> PhotoId { get; set; }
        public string PhotoPath { get; set; }
        public Nullable<System.DateTime> LastUpdated { get; set; }
        public string MLSID { get; set; }
    }
}