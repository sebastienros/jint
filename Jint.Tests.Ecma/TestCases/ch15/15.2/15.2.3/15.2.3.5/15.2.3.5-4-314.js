/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-314.js
 * @description Object.create - some enumerable own property in 'Properties' is empty object (15.2.3.7 step 7)
 */


function testcase() {

        var newObj = Object.create({}, {
            foo: {}
        });
        return newObj.hasOwnProperty("foo");
    }
runTestCase(testcase);
