using getsolddata.Modal;
using librets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Spatial;
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
        public static SqlConnection con = null;

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

            //Console.ReadLine();
        }
        public class OldMls
        {
            public string Mls { get; set; }
            public string Status { get; set; }
        }
        private static void processing(List<string> classTypes, RetsSession session)
        {
            List<PhotoDownlaodObject> photoDownlaodObjects = new List<PhotoDownlaodObject>();
            Dictionary<string, string> Tablenames = new Dictionary<string, string>();
            Tablenames.Add("ResidentialProperty", "vow_res_data");
            Tablenames.Add("CondoProperty", "vow_condo_data");
            Tablenames.Add("update", "updatetable");
            List<OldMls> oldmls = new List<OldMls>();
            string strcon = ConfigurationManager.ConnectionStrings["local"].ToString();
            con = new SqlConnection(strcon);

            if (con.State == ConnectionState.Closed)
            {
                con.Open();
            }
            SqlCommand cmd = new SqlCommand(@"select mlsid, left(propertystatus,1) propertystatus from PropertyDetails where mlsid is not null and PropertyStatus is not null group by mlsid , PropertyStatus", con);
            var rder = cmd.ExecuteReader();
            while (rder.Read())
            {
                oldmls.Add(new OldMls
                {
                    Mls = rder.GetString(0),
                    Status = rder.GetString(1)
                });
            }

            var statuslist = new List<string> { "U", "A" };
            foreach (string status in statuslist)
            {
                RetsVersion version = session.GetDetectedRetsVersion();
                string query = $"(Status = {status}),(TimestampSql= {DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")}+)";//
                                                                                                                          //string query = "(TimestampSql= Sept 18, 2019)";


                foreach (string classType in classTypes)
                {
                    SearchRequest searchRequest = session.CreateSearchRequest("Property", classType, query);
                    searchRequest.SetStandardNames(true);
                    searchRequest.SetQueryType(SearchRequest.QueryType.DMQL);
                    searchRequest.SetCountType(SearchRequest.CountType.NO_RECORD_COUNT);
                    searchRequest.SetFormatType(SearchRequest.FormatType.COMPACT);
                    SearchResultSet results = session.Search(searchRequest);
                    IEnumerable columns = results.GetColumns();
                    Dictionary<string, string> _dictRETS = new Dictionary<string, string>();
                    Dictionary<string, string> _dictRETSUpdate = new Dictionary<string, string>();
                    List<ModalLocation> forlocation = new List<ModalLocation>();
                    List<Dictionary<string, string>> _dictRETSlist = new List<Dictionary<string, string>>();
                    List<Dictionary<string, string>> _dictRETSUpdatelist = new List<Dictionary<string, string>>();
                    int num = 0;
                    while (results.HasNext())
                    {
                        num++;
                        string mls, newstatus;
                        try
                        {
                            mls = results.GetString("MLS");
                            var cc = results.GetColumns();
                            newstatus = results.GetString("Status");
                        }
                        catch (Exception ex)
                        {
                            continue;
                        }
                        var ch1 = oldmls.Where(w => w.Mls == mls & w.Status == newstatus);
                        if (newstatus == "U" || ch1 == null)
                        {
                            if (newstatus == "U" && ch1 != null)
                            {
                                
                                _dictRETS = new Dictionary<string, string>();
                                foreach (string str5 in new List<string>()
                                {
                                    "SoldPrice",
                                    "SoldDate",
                                    "Status",
                                    "MLS",
                                    "TimestampSql"
                                })
                                {
                                    try
                                    {
                                        _dictRETS.Add(str5, results.GetString(str5));
                                    }
                                    catch (Exception exception)
                                    {
                                        Console.WriteLine(exception.Message);
                                        Console.WriteLine("______________________________________");
                                    }
                                }
                                //var soldprice=results.GetString("SoldPrice");
                                //if (_dictRETS["SoldPrice"] != null && _dictRETS["SoldPrice"].Trim() != string.Empty)
                                {
                                    _dictRETSUpdatelist.Add(_dictRETS);
                                    if (_dictRETSUpdatelist.Count >= 20)
                                    {
                                        try
                                        {
                                            DataTable dataTable = ListtoDataTableConverter.ToDataTable(_dictRETSUpdatelist);
                                            Task.Factory.StartNew(() => Program.BulkInsert(dataTable, Tablenames["update"]));
                                            _dictRETSUpdatelist.Clear();
                                        }
                                        catch (Exception exception1)
                                        {
                                            Console.WriteLine(exception1.Message);
                                            Console.WriteLine("______________________________________");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                _dictRETS = new Dictionary<string, string>();
                                foreach (string column in columns)
                                {
                                    try
                                    {
                                        _dictRETS.Add(column, results.GetString(column));
                                    }
                                    catch (Exception exception2)
                                    {
                                        Console.WriteLine(exception2.Message);
                                        Console.WriteLine("______________________________________");
                                    }
                                }
                                if ((mls == null ? false : mls != string.Empty))
                                {
                                    _dictRETSlist.Add(_dictRETS);
                                    string item = _dictRETS["TimestampSql"];
                                    try
                                    {
                                        SetGetLatLongInProperty(new ModalLocation()
                                        {
                                            MLS = mls,
                                            City = _dictRETS["Area"],
                                            State = _dictRETS["Province"],
                                            Zip = _dictRETS["PostalCode"],
                                            Address = _dictRETS["Address"],
                                            Country = "Canada"
                                        }, _dictRETS);
                                    }
                                    catch (Exception exception3)
                                    {
                                    }
                                    if (_dictRETSlist.Count >= 20)
                                    {
                                        try
                                        {
                                            DataTable dataTable1 = ListtoDataTableConverter.ToDataTable(_dictRETSlist);
                                            Task.Factory.StartNew(() => Program.BulkInsert(dataTable1, Tablenames[classType]));
                                            _dictRETSlist.Clear();
                                        }
                                        catch (Exception exception4)
                                        {
                                            Console.WriteLine(exception4.Message);
                                            Console.WriteLine("______________________________________");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("in else");
                        }
                        _dictRETS = null;

                        Console.WriteLine(num);
                    }
                    if (_dictRETSlist.Count > 0)
                    {
                        DataTable dataTable2 = ListtoDataTableConverter.ToDataTable(_dictRETSlist);
                        Task.Factory.StartNew(() => Program.BulkInsert(dataTable2, Tablenames[classType]));
                    }
                    if (_dictRETSUpdatelist.Count > 0)
                    {
                        DataTable dataTable3 = ListtoDataTableConverter.ToDataTable(_dictRETSUpdatelist);
                        Task.Factory.StartNew(() => Program.BulkInsert(dataTable3, Tablenames["update"]));
                    }
                    if (photoDownlaodObjects.Count > 0)
                    {
                        DownloadAllListingPhotos(photoDownlaodObjects, session);
                    }
                }

                //searchRequest.SetStandardNames(true);
                //searchRequest.SetQueryType(SearchRequest.QueryType.DMQL);
                //searchRequest.SetCountType(SearchRequest.CountType.NO_RECORD_COUNT);
                //searchRequest.SetFormatType(SearchRequest.FormatType.COMPACT);
                //SearchResultSet results = session.Search(searchRequest);
                //IEnumerable columns = results.GetColumns();
                //Dictionary<string, string> _dictRETS = new Dictionary<string, string>();
                //Dictionary<string, string> _dictRETSUpdate = new Dictionary<string, string>();
                //List<ModalLocation> forlocation = new List<ModalLocation>();
                //List<Dictionary<string, string>> _dictRETSlist = new List<Dictionary<string, string>>();
                //List<Dictionary<string, string>> _dictRETSUpdatelist = new List<Dictionary<string, string>>();
                ////if (isCompareListing)
                ////{
                ////    recordprocessed += CompareListing(results, Ownertype);

                ////}
                ////else
                //{

                //    var imgdownloadlist = new List<PhotoDownlaodObject>();
                //    int recordprocessed = 0;
                //    //int rowcount = results.GetCount();
                //    //var ch = results.HasMaxRows();
                //    while (results.HasNext())
                //    {
                //        recordprocessed++;
                //        var mls = results.GetString("MLS");
                //        var newstatus = results.GetString("Status");
                //        var ch1 = oldmls.Where(w => w.Mls == mls & w.Status == newstatus);
                //        if ((newstatus == "U" || ch1 == null))
                //        {
                //            if ((newstatus == "U" && ch1 == null)) //
                //            {

                //                _dictRETS = new Dictionary<string, string>();

                //                foreach (string column in columns)
                //                {
                //                    try
                //                    {
                //                        _dictRETS.Add(column, results.GetString(column));
                //                    }
                //                    catch (Exception ex)
                //                    {
                //                        Console.WriteLine(ex.Message);
                //                        Console.WriteLine("______________________________________");
                //                    }

                //                }


                //                if (mls!= null && mls != string.Empty)
                //                {


                //                    _dictRETSlist.Add(_dictRETS);
                //                    var data = _dictRETS["TimestampSql"];
                //                    var apiaddress = new ModalLocation
                //                    {
                //                        MLS = mls,
                //                        City = _dictRETS["Area"],
                //                        State = _dictRETS["Province"],
                //                        Zip = _dictRETS["PostalCode"],
                //                        Address = _dictRETS["Address"],
                //                        Country = "Canada"
                //                    };
                //                    SetGetLatLongInProperty(apiaddress, _dictRETS);
                //                    //if (DateTime.Parse(data) >=DateTime.Now.AddMonths(-6))
                //                    //{

                //                    //    imgdownloadlist.Add(
                //                    //        new PhotoDownlaodObject()
                //                    //        {
                //                    //            MLSID = _dictRETS["MLS"],
                //                    //            LastUpdated = DateTime.Now
                //                    //        });

                //                    //}
                //                    if (_dictRETSlist.Count >= 20)
                //                    {
                //                        try
                //                        {

                //                            var table = ListtoDataTableConverter.ToDataTable(_dictRETSlist);
                //                            Task.Factory.StartNew(() =>
                //                            {
                //                                BulkInsert(table, "vobdata");
                //                            });

                //                            _dictRETSlist.Clear();
                //                        }
                //                        catch (Exception ex)
                //                        {
                //                            Console.WriteLine(ex.Message);
                //                            Console.WriteLine("______________________________________");
                //                        }
                //                    }
                //                }
                //            }
                //            else
                //            {
                //                _dictRETSUpdate = new Dictionary<string, string>();
                //                List<string> collist = new List<string> { "SoldPrice", "SoldDate", "Status", "MLS", "TimestampSql" };
                //                foreach (string column in collist)
                //                {
                //                    try
                //                    {
                //                        _dictRETSUpdate.Add(column, results.GetString(column));
                //                    }
                //                    catch (Exception ex)
                //                    {
                //                        Console.WriteLine(ex.Message);
                //                        Console.WriteLine("______________________________________");
                //                    }

                //                }


                //                if (_dictRETSUpdate["SoldPrice"] != null && _dictRETSUpdate["SoldPrice"].Trim() != string.Empty)
                //                {


                //                    _dictRETSUpdatelist.Add(_dictRETSUpdate);
                //                    //var data = _dictRETS["TimestampSql"];
                //                    //var apiaddress = new ModalLocation
                //                    //{
                //                    //    MLS = mls,
                //                    //    City = _dictRETS["Area"],
                //                    //    State = _dictRETS["Province"],
                //                    //    Zip = _dictRETS["PostalCode"],
                //                    //    Address = _dictRETS["Address"],
                //                    //    Country = "Canada"
                //                    //};
                //                    //SetGetLatLongInProperty(apiaddress, _dictRETS);
                //                    //if (DateTime.Parse(data) >=DateTime.Now.AddMonths(-6))
                //                    //{

                //                    //    imgdownloadlist.Add(
                //                    //        new PhotoDownlaodObject()
                //                    //        {
                //                    //            MLSID = _dictRETS["MLS"],
                //                    //            LastUpdated = DateTime.Now
                //                    //        });

                //                    //}
                //                    if (_dictRETSUpdatelist.Count >= 20)
                //                    {
                //                        try
                //                        {

                //                            var table = ListtoDataTableConverter.ToDataTable(_dictRETSUpdatelist);
                //                            Task.Factory.StartNew(() =>
                //                            {
                //                                BulkInsert(table,"updatetable");
                //                            });

                //                            _dictRETSUpdatelist.Clear();
                //                        }
                //                        catch (Exception ex)
                //                        {
                //                            Console.WriteLine(ex.Message);
                //                            Console.WriteLine("______________________________________");
                //                        }
                //                    }
                //                }

                //            }
                //        }
                //        else
                //        {

                //        }
                //        _dictRETS = null;
                //        _dictRETSUpdate = null;
                //        Console.WriteLine(recordprocessed);

                //    }
                //    if (_dictRETSlist.Count > 0)
                //    {

                //        var table1 = ListtoDataTableConverter.ToDataTable(_dictRETSlist);
                //        Task.Factory.StartNew(() =>
                //        {
                //            BulkInsert(table1,"vobdata");
                //        });

                //    }
                //    if (_dictRETSUpdatelist.Count > 0)
                //    {

                //        var table1 = ListtoDataTableConverter.ToDataTable(_dictRETSUpdatelist);
                //        Task.Factory.StartNew(() =>
                //        {
                //            BulkInsert(table1, "updatetable");
                //        });

                //    }
                //    if (imgdownloadlist.Count > 0)
                //        DownloadAllListingPhotos(imgdownloadlist, session);


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
                // }
                // break;
            }
            cmd = new SqlCommand("mergevowdata", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.ExecuteNonQuery();

        }


        private static void SetGetLatLongInProperty(ModalLocation apiAddress, Dictionary<string, string> dictRETS)
        {

            var addressString = $" Address = [{apiAddress.Address}] City = [{apiAddress.City}] State = [{apiAddress.State}] Zip = [{apiAddress.Zip}] Country = [{apiAddress.Country}]";
            try
            {
                //LocationAPIhitCount++;
                bool gotLatLong = false;

                #region Old COde

                // var latlong = gls.GetLatLongFromAddress(apiAddress);
                var latlong = new RealEstate.AzureMap.SearchAddress().GetLatLong(apiAddress);
                if (latlong != null)
                {
                    if (!string.IsNullOrEmpty(latlong.Latitude.ToString()))
                    {
                        if (latlong.Latitude != 0 && latlong.Longitude != 0)
                        {
                            gotLatLong = true;
                            dictRETS.Add("Latitude", latlong.Latitude.ToString());
                            dictRETS.Add("Longitude", latlong.Longitude.ToString());
                            //var location = $"Point({latlong.Longitude} {latlong.Latitude})";
                            //dictRETS.Add("GeoData", DbGeometry.PointFromText(location, 4326));

                        }
                    }

                }
                #endregion

            }
            catch (Exception ex)
            {

            }
        }

        public static void BulkInsert(DataTable dt, string table)
        {

            Console.WriteLine($"comming to save record count {dt.Rows.Count}");

            {
                if (con.State == ConnectionState.Closed)
                {
                    con.Open();
                }
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(con))
                {
                    try
                    {

                        bulkCopy.DestinationTableName = table;
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
                foreach (DataColumn key in dataTable.Columns)
                {
                    if (key.ColumnName == "ID")
                    {
                        continue;
                    }
                    values[i] = item[key.ColumnName];
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