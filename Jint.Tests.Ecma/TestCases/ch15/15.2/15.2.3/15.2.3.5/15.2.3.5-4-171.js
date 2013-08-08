/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-171.js
 * @description Object.create - one property in 'Properties' is a Date object that uses Object's [[Get]] method to access the 'value' property (8.10.5 step 5.a)
 */


function testcase() {

        var dateObj = new Date();

        dateObj.value = "DateValue";

        var newObj = Object.create({}, {
            prop: dateObj
        });

        return newObj.prop === "DateValue";
    }
runTestCase(testcase);
