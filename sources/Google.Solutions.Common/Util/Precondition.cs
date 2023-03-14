﻿//
// Copyright 2023 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using System;
using System.Diagnostics;

namespace Google.Solutions.Common.Util
{
    public static class Precondition
    {
        public static T ExpectNotNull<T>(
            this T value,
            string argumentName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(
                    $"The argument {argumentName} must not be null");
            }

            return value;
        }

        public static void Expect(
            bool condition,
            string message)
        {
            if (!condition)
            {
                throw new ArgumentException(message);
            }
        }

        public static float ExpectInRange(
            this float value,
            float min,
            float max,
            string argumentName)
        {
            Debug.Assert(min < max);
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(
                    $"The argument {argumentName} must be between {min} and {max}");
            }

            return value;
        }
    }
}
