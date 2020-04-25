using getsolddata.Modal;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace getsolddata.Process
{
    public class GetPropertyLocation
    {
        public static SqlConnection con = null;

        public GetPropertyLocation()
        {

            string strcon = ConfigurationManager.ConnectionStrings["local"].ToString();
            con = new SqlConnection(strcon);

            if (con.State == ConnectionState.Closed)
            {
                con.Open();
            }
        }
        public class updatelocation
        {
            public string id { get; set; }
            public string mls { get; set; }
            public string lat { get; set; }
            public string lang { get; set; }

        }
        public void Process()
        {
            try
            {
                SqlCommand cmd = new SqlCommand("select mls,address,city,state,zip,id from propertydetails where datasource=2 and isnull(Latitude,'')=''  and isnull(Longitude,'')=''",con);
                var reader=cmd.ExecuteReader();
                while(reader.Read())
                {
                    var apiaddress = new ModalLocation
                    {
                        MLS = reader.GetString(0),
                        Address = reader.GetString(1),
                        City = reader.GetString(2),
                        State = reader.GetString(3),
                        Zip = reader.GetString(4),
                        Id=reader.GetString(5),
                        Country = "Canada"
                    };
                }
            }
            catch (Exception ex)
            {

            }
        }
        private  void SetGetLatLongInProperty(ModalLocation apiAddress, Dictionary<string, string> dictRETS)
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

    }
}
