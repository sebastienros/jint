/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-159.js
 * @description Object.defineProperties - 'descObj' is the JSON object which implements its own [[Get]] method to get 'writable' property (8.10.5 step 6.a)
 */


function testcase() {

        var obj = {};

        try {
            JSON.writable = false;

            Object.defineProperties(obj, {
                property: JSON
            });

            obj.property = "isWritable";

            return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
        } finally {
            delete JSON.writable;
        }
    }
runTestCase(testcase);
