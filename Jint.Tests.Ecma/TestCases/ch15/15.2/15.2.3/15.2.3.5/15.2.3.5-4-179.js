/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-179.js
 * @description Object.create - 'writable' property of one property in 'Properties' is not present (8.10.5 step 6)
 */


function testcase() {

        var newObj = Object.create({}, {
            prop: {
                value: 100
            }
        });

        var beforeWrite = (newObj.prop === 100);

        newObj.prop = "isWritable";

        var afterWrite = (newObj.prop === 100);

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
