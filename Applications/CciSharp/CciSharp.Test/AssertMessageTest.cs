﻿//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace CciSharp.Test
{
    public partial class AssertMessageTest
    {
        [Fact]
        public void AddMessageToTrue2Variables()
        {
            int x = 3;
            int y = 7;
            try
            {
                Assert.True(x != y);
            }
            catch (Exception ex)
            {
                Assert.True(ex.Message.Contains("x != y"), ex.Message + "does not contain the expression");
            }
        }

        [Fact]
        public void AddMessageToTrue2MultipleVariables()
        {
            int x = 3;
            int y = 7;
            try
            {
                Assert.True(x != y && y * y < 10);
            }
            catch (Exception ex)
            {
                Assert.True(ex.Message.Contains("x != y && y * y < 10"), ex.Message + "does not contain the expression");
            }
        }

        [Fact]
        public void AddMessageToTrue()
        {
            int x = 3;
            try
            {
                Assert.True(x != 5);
            }
            catch (Exception ex)
            {
                Assert.True(ex.Message.Contains("x != 5"), ex.Message + "does not contain the expression");
            }
        }

        [Fact]
        public void AddLargeExpressionToTrue()
        {
            try
            {
                Assert.True(this.ToString() == null);
            }
            catch (Exception ex)
            {
                Assert.True(ex.Message.Contains("this.ToString() == null"), ex.Message + "does not contain the expression");
            }
        }
    }
}
