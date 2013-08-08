/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-249.js
 * @description Object.defineProperties - 'descObj' is the Arguments object which implements its own [[Get]] method to get 'set' property (8.10.5 step 8.a)
 */


function testcase() {
        var data = "data";
        var fun = function () {
            return arguments;
        };
        var arg = fun();
        var setFun = function (value) {
            data = value;
        };

        arg.prop = {
            set: setFun
        };

        var obj = {};
        Object.defineProperties(obj, arg);
        obj.prop = "argData";
        return obj.hasOwnProperty("prop") && data === "argData";
    }
runTestCase(testcase);
