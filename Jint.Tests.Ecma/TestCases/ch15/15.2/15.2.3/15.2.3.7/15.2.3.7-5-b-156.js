/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-156.js
 * @description Object.defineProperties - 'descObj' is the Math object which implements its own [[Get]] method to get 'writable' property (8.10.5 step 6.a)
 */


function testcase() {

        var obj = {};

        try {
            Math.writable = false;

            Object.defineProperties(obj, {
                property: Math
            });

            obj.property = "isWritable";

            return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
        } finally {
            delete Math.writable;
        }
    }
runTestCase(testcase);
