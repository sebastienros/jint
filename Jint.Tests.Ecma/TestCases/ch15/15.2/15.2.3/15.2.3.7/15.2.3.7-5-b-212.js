/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-212.js
 * @description Object.defineProperties - 'descObj' is the JSON object which implements its own [[Get]] method to get 'get' property (8.10.5 step 7.a)
 */


function testcase() {

        var obj = {};

        try {
            JSON.get = function () {
                return "JSON";
            };

            Object.defineProperties(obj, {
                property: JSON
            });

            return obj.property === "JSON";
        } finally {
            delete JSON.get;
        }
    }
runTestCase(testcase);
