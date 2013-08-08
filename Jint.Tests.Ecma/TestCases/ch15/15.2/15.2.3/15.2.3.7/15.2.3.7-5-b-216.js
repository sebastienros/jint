/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-216.js
 * @description Object.defineProperties - 'descObj' is the global object which implements its own [[Get]] method to get 'get' property (8.10.5 step 7.a)
 */


function testcase() {

        var obj = {};

        try {
            fnGlobalObject().get = function () {
                return "global";
            };

            Object.defineProperties(obj, {
                property: fnGlobalObject()
            });

            return obj.property === "global";
        } finally {
            delete fnGlobalObject().get;
        }
    }
runTestCase(testcase);
