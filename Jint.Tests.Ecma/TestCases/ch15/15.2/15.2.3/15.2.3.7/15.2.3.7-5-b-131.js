/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-131.js
 * @description Object.defineProperties - 'descObj' is a Date object which implements its own [[Get]] method to get 'value' property (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        var descObj = new Date();

        descObj.value = "Date";

        Object.defineProperties(obj, {
            property: descObj
        });

        return obj.property === "Date";
    }
runTestCase(testcase);
