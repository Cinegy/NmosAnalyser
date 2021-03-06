﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace NmosAnalyser
{

    [ServiceContract]
    public interface INmosAnalyserApi
    {
        [OperationContract]
        [WebInvoke(Method = "OPTIONS", UriTemplate = "/*")]
        void GetGlobalOptions();

        [OperationContract]
        [WebGet(UriTemplate = "/*")]
        Stream ServeEmbeddedStaticFile();

        [OperationContract]
        [WebGet(UriTemplate = "/V1/CurrentMetrics")]
        SerialisableMetrics GetCurrentMetrics();

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/ResetMetrics")]
        void ResetMetrics();
        
        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/Start")]
        void StartStream();

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/V1/Stop")]
        void StopStream();

    }


}
