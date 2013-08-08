/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-204.js
 * @description Object.defineProperties - 'descObj' is a Function object which implements its own [[Get]] method to get 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var func = function (a, b) {
            return a + b;
        };

        func.get = function () {
            return "Function";
        };

        Object.defineProperties(obj, {
            property: func
        });

        return obj.property === "Function";
    }
runTestCase(testcase);
