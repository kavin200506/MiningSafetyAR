// Vendored from QRCoder v1.4.3 (https://github.com/codebude/QRCoder), MIT License.
// Copyright (c) 2013-2018 Raffael Herrmann. See LICENSE.txt in this folder.
// Trimmed to the module-matrix container only (raw byte-stream/compression constructors removed, unused here).

using System.Collections;
using System.Collections.Generic;

namespace QRCoder
{
    public class QRCodeData
    {
        public List<BitArray> ModuleMatrix { get; set; }

        public QRCodeData(int version)
        {
            this.Version = version;
            var size = ModulesPerSideFromVersion(version);
            this.ModuleMatrix = new List<BitArray>();
            for (var i = 0; i < size; i++)
                this.ModuleMatrix.Add(new BitArray(size));
        }

        public int Version { get; private set; }

        private static int ModulesPerSideFromVersion(int version)
        {
            return 21 + (version - 1) * 4;
        }
    }
}
