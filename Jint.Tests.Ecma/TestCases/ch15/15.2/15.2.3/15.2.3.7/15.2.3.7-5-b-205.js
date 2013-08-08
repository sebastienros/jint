/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-205.js
 * @description Object.defineProperties - 'descObj' is an Array object which implements its own [[Get]] method to get 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var arr = [];

        arr.get = function () {
            return "Array";
        };

        Object.defineProperties(obj, {
            property: arr
        });

        return obj.property === "Array";
    }
runTestCase(testcase);
