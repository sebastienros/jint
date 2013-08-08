/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-213.js
 * @description Object.defineProperties - 'descObj' is an Error object which implements its own [[Get]] method to get 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var descObj = new Error();

        descObj.get = function () {
            return "Error";
        };

        Object.defineProperties(obj, {
            property: descObj
        });

        return obj.property === "Error";
    }
runTestCase(testcase);
