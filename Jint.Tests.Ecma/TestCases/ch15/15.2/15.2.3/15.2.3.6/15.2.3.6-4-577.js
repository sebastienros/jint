/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-577.js
 * @description ES5 Attributes - [[Set]] attribute is a function which involves 'this' object into statement(s)
 */


function testcase() {
        var obj = {};

        var setFunc = function (value) {
            this.len = value;
        };

        Object.defineProperty(obj, "prop", {
            set: setFunc
        });
        obj.prop = 2010;

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.hasOwnProperty("prop") && desc.set === setFunc && obj.len === 2010;
    }
runTestCase(testcase);
