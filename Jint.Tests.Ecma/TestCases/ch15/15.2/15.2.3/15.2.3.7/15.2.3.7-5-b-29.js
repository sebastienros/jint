/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-29.js
 * @description Object.defineProperties - 'descObj' is the Arguments object which implements its own [[Get]] method to get 'enumerable' property (8.10.5 step 3.a)
 */


function testcase() {

        var obj = {};
        var arg;
        var accessed = false;

        (function fun() {
            arg = arguments;
        }());

        arg.enumerable = true;

        Object.defineProperties(obj, {
            prop: arg
        });
        for (var property in obj) {
            if (property === "prop") {
                accessed = true;
            }
        }
        return accessed;
    }
runTestCase(testcase);
