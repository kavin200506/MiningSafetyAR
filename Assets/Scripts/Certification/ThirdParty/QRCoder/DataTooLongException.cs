// Vendored from QRCoder v1.4.3 (https://github.com/codebude/QRCoder), MIT License.
// Copyright (c) 2013-2018 Raffael Herrmann. See LICENSE.txt in this folder.

﻿using System;

namespace QRCoder.Exceptions
{
    public class DataTooLongException : Exception
    {
        public DataTooLongException(string eccLevel, string encodingMode, int maxSizeByte) : base(
            $"The given payload exceeds the maximum size of the QR code standard. The maximum size allowed for the choosen paramters (ECC level={eccLevel}, EncodingMode={encodingMode}) is {maxSizeByte} byte."
        ){}

        public DataTooLongException(string eccLevel, string encodingMode, int version, int maxSizeByte) : base(
            $"The given payload exceeds the maximum size of the QR code standard. The maximum size allowed for the choosen paramters (ECC level={eccLevel}, EncodingMode={encodingMode}, FixedVersion={version}) is {maxSizeByte} byte."
        )
        { }
    }
}
