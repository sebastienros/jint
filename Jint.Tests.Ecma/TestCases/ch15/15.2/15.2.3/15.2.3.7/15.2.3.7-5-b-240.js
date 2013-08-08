/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-240.js
 * @description Object.defineProperties - 'descObj' is an Array object which implements its own [[Get]] method to get 'set' property (8.10.5 step 8.a)
 */


function testcase() {

        var data = "data";
        var arr = [];
        var setFun = function (value) {
            data = value;
        };
        arr.prop = {
            set: setFun
        };

        var obj = {};
        Object.defineProperties(obj, arr);
        obj.prop = "arrData";
        return obj.hasOwnProperty("prop") && data === "arrData";
    }
runTestCase(testcase);
