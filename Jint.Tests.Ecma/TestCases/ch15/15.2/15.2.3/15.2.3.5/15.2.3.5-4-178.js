/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-178.js
 * @description Object.create - 'writable' property of one property in 'Properties' is true (8.10.5 step 6)
 */


function testcase() {

        var newObj = Object.create({}, {
            prop: {
                writable: true
            }
        });

        var beforeWrite = ((newObj.hasOwnProperty("prop") && typeof (newObj.prop) === "undefined"));

        newObj.prop = "isWritable";

        var afterWrite = (newObj.prop === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
