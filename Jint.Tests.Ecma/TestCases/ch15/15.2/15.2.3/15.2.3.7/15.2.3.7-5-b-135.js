/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-135.js
 * @description Object.defineProperties - 'descObj' is the Arguments object which implements its own [[Get]] method to get 'value' property (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        var func = function (a, b) {
            arguments.value = "arguments";

            Object.defineProperties(obj, {
                property: arguments
            });

            return obj.property === "arguments";
        };

        return func();
    }
runTestCase(testcase);
