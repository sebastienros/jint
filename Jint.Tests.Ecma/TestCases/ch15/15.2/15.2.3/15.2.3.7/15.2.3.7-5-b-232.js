/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-232.js
 * @description Object.defineProperties - 'set' property of 'descObj' is own accessor property (8.10.5 step 8.a)
 */


function testcase() {
        var data = "data";
        var setFun = function (value) {
            data = value;
        };
        var descObj = {};
        Object.defineProperty(descObj, "set", {
            get: function () {
                return setFun;
            }
        });

        var obj = {};

        Object.defineProperties(obj, {
            prop: descObj
        });

        obj.prop = "overrideData";

        return obj.hasOwnProperty("prop") && data === "overrideData";

    }
runTestCase(testcase);
