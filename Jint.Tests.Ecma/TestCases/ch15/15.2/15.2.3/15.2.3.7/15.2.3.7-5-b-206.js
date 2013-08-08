/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-206.js
 * @description Object.defineProperties - 'descObj' is a String object which implements its own [[Get]] method to get 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var str = new String("abc");

        str.get = function () {
            return "string Object";
        };

        Object.defineProperties(obj, {
            property: str
        });

        return obj.property === "string Object";
    }
runTestCase(testcase);
