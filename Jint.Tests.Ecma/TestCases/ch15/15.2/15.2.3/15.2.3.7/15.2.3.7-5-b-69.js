/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-69.js
 * @description Object.defineProperties - 'configurable' property of 'descObj' is own accessor property without a get function (8.10.5 step 4.a)
 */


function testcase() {

        var obj = {};

        var descObj = {};
        Object.defineProperty(descObj, "configurable", {
            set: function () { }
        });

        Object.defineProperties(obj, {
            prop: descObj
        });
        var result1 = obj.hasOwnProperty("prop");
        delete obj.prop;
        var result2 = obj.hasOwnProperty("prop");

        return result1 === true && result2 === true;
    }
runTestCase(testcase);
