/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-167.js
 * @description Object.defineProperty - 'O' is an Array, 'name' is the length property of 'O', the [[Value]] field of 'desc' is less than value of  the length property, test the [[Writable]] attribute of the length property is set to false after deleting properties with large index named if the [[Writable]] field of 'desc' is false (15.4.5.1 step 3.i.ii)
 */


function testcase() {

        var arrObj = [0, 1];

        Object.defineProperty(arrObj, "length", {
            value: 1,
            writable: false
        });

        var indexDeleted = !arrObj.hasOwnProperty("1");

        arrObj.length = 10;

        return indexDeleted && arrObj.length === 1;
    }
runTestCase(testcase);
