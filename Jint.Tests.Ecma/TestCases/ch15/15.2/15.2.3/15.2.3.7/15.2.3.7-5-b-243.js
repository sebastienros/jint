/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-243.js
 * @description Object.defineProperties - 'descObj' is a Number object which implements its own [[Get]] method to get 'set' property (8.10.5 step 8.a)
 */


function testcase() {

        var data = "data";
        var descObj = new Number(-9);
        var setFun = function (value) {
            data = value;
        };
        descObj.prop = {
            set: setFun
        };

        var obj = {};
        Object.defineProperties(obj, descObj);
        obj.prop = "numberData";
        return obj.hasOwnProperty("prop") && data === "numberData";
    }
runTestCase(testcase);
