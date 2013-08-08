/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-261.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is an array index named property, name is data property and 'desc' is data descriptor, test updating the [[Writable]] attribute value of 'name' (15.4.5.1 step 4.c)
 */


function testcase() {

        var arrObj = [100];
        Object.defineProperty(arrObj, "0", {
            writable: false
        });
        return dataPropertyAttributesAreCorrect(arrObj, "0", 100, false, true, true);
    }
runTestCase(testcase);
