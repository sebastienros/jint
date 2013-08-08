/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-575.js
 * @description ES5 Attributes - [[Set]] attribute is a function which contains global variable
 */


function testcase() {
        var obj = {};
        var globalVariable = 20;
        var setFunc = function () {
            globalVariable = 2010;
        };

        Object.defineProperty(obj, "prop", {
            set: setFunc
        });
        obj.prop = 10;
        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.hasOwnProperty("prop") && desc.set === setFunc && globalVariable === 2010;
    }
runTestCase(testcase);
