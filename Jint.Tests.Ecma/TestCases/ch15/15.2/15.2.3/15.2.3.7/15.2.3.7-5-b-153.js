/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-153.js
 * @description Object.defineProperties - 'descObj' is a String object which implements its own [[Get]] method to get 'writable' property (8.10.5 step 6.a)
 */


function testcase() {
        var obj = {};

        var str = new String("abc");

        str.writable = false;

        Object.defineProperties(obj, {
            property: str
        });

        obj.property = "isWritable";

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
