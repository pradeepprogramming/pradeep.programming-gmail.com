using getsolddata.Modal;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace RealEstate.AzureMap
{
    public class MapPoint
    {
        
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    public class SearchAddress
    {
        public MapPoint GetLatLong(ModalLocation Address)
        {
            string address = "";
            if (!string.IsNullOrEmpty(Address.Address))
            {
                address += Address.Address;
            }
            if (!string.IsNullOrEmpty(Address.City))
            {
                if (!string.IsNullOrEmpty(address))
                {
                    address += ", ";
                }
                address += Address.City;
            }
            if (!string.IsNullOrEmpty(Address.State))
            {
                if (!string.IsNullOrEmpty(address))
                {
                    address += ", ";
                }
                address += Address.State;
            }
            if (!string.IsNullOrEmpty(Address.Country))
            {
                if (!string.IsNullOrEmpty(address))
                {
                    address += ", ";
                }
                address += Address.Country;
            }
            
            MapPoint mapPoint = null;
            address = new UrlHelper().Encode(address);
            var APIURL = $"https://atlas.microsoft.com/search/address/json?subscription-key={ConfigurationManager.AppSettings["locationapi"]}&api-version=1.0&query={address}&limit=1";
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(APIURL);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "GET";
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var jsonResult = streamReader.ReadToEnd();
                JavaScriptSerializer json_serializer = new JavaScriptSerializer();
                if (jsonResult.IndexOf("error") != -1)
                {
                    ErrorResult error = json_serializer.Deserialize<ErrorResult>(jsonResult);
                    throw new Exception("Error Code = " + error.error.code + " , Error Msg = " + error.error.message);
                }
                SearchResult routes_list = json_serializer.Deserialize<SearchResult>(jsonResult);
                if (routes_list.results != null)
                {
                    if (routes_list.results.Count > 0)
                    {
                        mapPoint = new MapPoint()
                        {
                            Latitude = (routes_list.results?.FirstOrDefault()?.position.lat) ?? 0,
                            Longitude = (routes_list.results?.FirstOrDefault()?.position.lon) ?? 0
                        };

                    }
                }
            }
            return mapPoint;
        }
        private class Summary
        {
            public string query { get; set; }
            public string queryType { get; set; }
            public int queryTime { get; set; }
            public int numResults { get; set; }
            public int offset { get; set; }
            public int totalResults { get; set; }
            public int fuzzyLevel { get; set; }
        }

        private class Address
        {
            public string streetNumber { get; set; }
            public string streetName { get; set; }
            public string municipalitySubdivision { get; set; }
            public string municipality { get; set; }
            public string countrySecondarySubdivision { get; set; }
            public string countryTertiarySubdivision { get; set; }
            public string countrySubdivision { get; set; }
            public string postalCode { get; set; }
            public string extendedPostalCode { get; set; }
            public string countryCode { get; set; }
            public string country { get; set; }
            public string countryCodeISO3 { get; set; }
            public string freeformAddress { get; set; }
            public string countrySubdivisionName { get; set; }
        }

        private class Position
        {
            public double lat { get; set; }
            public double lon { get; set; }
        }

        private class TopLeftPoint
        {
            public double lat { get; set; }
            public double lon { get; set; }
        }

        private class BtmRightPoint
        {
            public double lat { get; set; }
            public double lon { get; set; }
        }

        private class Viewport
        {
            public TopLeftPoint topLeftPoint { get; set; }
            public BtmRightPoint btmRightPoint { get; set; }
        }

        private class Position2
        {
            public double lat { get; set; }
            public double lon { get; set; }
        }

        private class EntryPoint
        {
            public string type { get; set; }
            public Position2 position { get; set; }
        }

        private class Result
        {
            public string type { get; set; }
            public string id { get; set; }
            public double score { get; set; }
            public Address address { get; set; }
            public Position position { get; set; }
            public Viewport viewport { get; set; }
            public List<EntryPoint> entryPoints { get; set; }
        }

        private class SearchResult
        {
            public Summary summary { get; set; }
            public List<Result> results { get; set; }
        }
        private class Error
        {
            public string code { get; set; }
            public string message { get; set; }
        }

        private class ErrorResult
        {
            public Error error { get; set; }
        }
    }
}