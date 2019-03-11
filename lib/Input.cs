/*
 * Copyright 2019 Tamas Bolner
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;

namespace AsyncFastCGI
{
    class Input {
        private byte[] binaryContent;
        private Dictionary<string, string> parameters;

        public Input(byte[] binaryContent, Dictionary<string, string> parameters)
        {
            this.binaryContent = binaryContent;
            this.parameters = parameters;
        }

        public string getContent() {
            return System.Text.Encoding.UTF8.GetString(this.binaryContent);
        }

        public byte[] getBinaryContent() {
            return binaryContent;
        }

        public int getHttpStatus() {
            return 200;
        }

        public string getHeader(string name) {
            return "";
        }

        /// <summary>
        /// Returns the value of a server parameter.
        /// See the full list of passed parameters in:
        ///     - /etc/nginx/fastcgi_params
        /// </summary>
        /// <param name="name">The name of the parameter</param>
        /// <returns>The parameter value</returns>
        public string getParameter(string name) {
            return this.parameters[name];
        }
    }
}
