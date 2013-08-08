/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-26.js
 * @description Object.defineProperties - 'P' doesn't exist in 'O', test 'P' is defined as data property when 'desc' is generic descriptor (8.12.9 step 4.a)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            prop: {
                configurable: true,
                enumerable: true
            }
        });
        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return desc.hasOwnProperty("value") && typeof desc.value === "undefined" &&
            desc.hasOwnProperty("writable") && desc.writable === false &&
            desc.hasOwnProperty("configurable") && desc.configurable === true &&
            desc.hasOwnProperty("enumerable") && desc.enumerable === true &&
            !desc.hasOwnProperty("get") && !desc.hasOwnProperty("set");
    }
runTestCase(testcase);
