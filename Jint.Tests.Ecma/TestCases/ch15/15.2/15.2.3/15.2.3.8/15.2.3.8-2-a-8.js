/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-8.js
 * @description Object.seal - 'P' is own property of an Array object that uses Object's [[GetOwnProperty]]
 */


function testcase() {
        var arrObj = [];

        arrObj.foo = 10;
        var preCheck = Object.isExtensible(arrObj);
        Object.seal(arrObj);

        delete arrObj.foo;
        return preCheck && arrObj.foo === 10;
    }
runTestCase(testcase);
