﻿//
// Copyright 2020 Google LLC
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
using System.Security;

namespace Google.Solutions.IapDesktop.Application.Profile.Settings
{
    /// <summary>
    /// Base interface for a setting.
    /// </summary>
    public interface ISetting
    {
        /// <summary>
        /// Unique, stable key.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Name of setting, suitable for displaying.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Description of setting, suitable for displaying.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Category of setting, suitable for displaying.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Return current value of setting.
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// Determines whether the current value is equivalent to
        /// the default value.
        /// </summary>
        bool IsDefault { get; }

        /// <summary>
        /// Reset value to default.
        /// </summary>
        void Reset();

        /// <summary>
        /// Determines if the value has been changed and needs
        /// to be written back to the repository.
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// Determines whether the user has modified this setting.
        /// </summary>
        bool IsSpecified { get; }

        /// <summary>
        /// Determines if the user is allowed to change the setting
        /// (or whether it's mandated by a policy).
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Overlay the setting with defaults from the ancestry,
        /// </summary>
        ISetting OverlayBy(ISetting setting);

        /// <summary>
        /// Returns the type of setting.
        /// </summary>
        Type ValueType { get; }
    }

    public interface ISetting<T> : ISetting
    {
        /// <summary>
        /// Returns the typed default value.
        /// </summary>
        T DefaultValue { get; }

        /// <summary>
        /// Overlay the setting with defaults from the ancestry,
        /// </summary>
        ISetting<T> OverlayBy(ISetting<T> setting);
    }

    /// <summary>
    /// String-valued setting.
    /// </summary>
    public interface IStringSetting : ISetting<string>
    {
        string StringValue { get; set; }
    }

    /// <summary>
    /// SecureString-valued setting.
    /// </summary>
    public interface ISecureStringSetting : ISetting<SecureString>
    {
        string ClearTextValue { get; set; }
    }

    /// <summary>
    /// Bool-valued setting.
    /// </summary>
    public interface IBoolSetting : ISetting<bool>
    {
        bool BoolValue { get; set; }
    }

    /// <summary>
    /// Int-valued setting.
    /// </summary>
    public interface IIntSetting : ISetting<int>
    {
        int IntValue { get; set; }
    }

    /// <summary>
    /// Long-valued setting.
    /// </summary>
    public interface ILongSetting : ISetting<long>
    {
        long LongValue { get; set; }
    }

    /// <summary>
    /// Enum-valued setting.
    /// </summary>
    public interface IEnumSetting<TEnum> : ISetting<TEnum>
        where TEnum : struct
    {
        TEnum EnumValue { get; set; }
    }
}
