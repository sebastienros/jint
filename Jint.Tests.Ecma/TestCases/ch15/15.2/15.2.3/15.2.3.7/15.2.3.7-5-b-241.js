/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-241.js
 * @description Object.defineProperties - 'descObj' is a String object which implements its own [[Get]] method to get 'set' property (8.10.5 step 8.a)
 */


function testcase() {
        var data = "data";
        var descStr = new String();
        var setFun = function (value) {
            data = value;
        };

        descStr.prop = {
            set: setFun
        };

        var obj = {};
        Object.defineProperties(obj, descStr);
        obj.prop = "strData";
        return obj.hasOwnProperty("prop") && data === "strData";
    }
runTestCase(testcase);
