/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-14.js
 * @description Object.seal - 'P' is own property of an Error object that uses Object's [[GetOwnProperty]]
 */


function testcase() {
        var errObj = new Error();

        errObj.foo = 10;
        var preCheck = Object.isExtensible(errObj);
        Object.seal(errObj);

        delete errObj.foo;
        return preCheck && errObj.foo === 10;
    }
runTestCase(testcase);
