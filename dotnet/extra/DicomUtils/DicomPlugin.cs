// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace DicomUtils
{
    public class DicomPlugin
    {
        /// <summary>
        /// Finds and return string containg UltrasoundRegions of the image
        /// </summary>
        /// <param name="dicomFileId"></param>
        [KernelFunction]
        [Description("Returns string representing json containing [(0018,6011) Sequence of Ultrasound Regions] extracted from dicom file.")]
        public Task<string> RegionsJsonAsync(
            [Description("Id of the dicom file.")] long dicomFileId)
        {
            var json = File.ReadAllText("TestData/cdf069b023ea4e33b5c78ac1eff45370.ultrasound-regions.json");
            return Task.FromResult(json);
        }
    }
}
