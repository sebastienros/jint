/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-16.js
 * @description Object.create - own enumerable data property in 'Properties' is defined in 'obj' (15.2.3.7 step 3)
 */


function testcase() {

        var newObj = Object.create({}, {
            prop: {} 
        });
        return newObj.hasOwnProperty("prop");
    }
runTestCase(testcase);
