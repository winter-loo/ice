// **********************************************************************
//
// Copyright (c) 2003-present ZeroC, Inc. All rights reserved.
//
// **********************************************************************

namespace Ice
{
    namespace objects
    {
        public sealed class II : Ice.InterfaceByValue
        {
            public II() : base(Test.IDisp_.ice_staticId())
            {
            }
        }
    }
}
