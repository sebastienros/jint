/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-10.js
 * @description Object.seal - 'P' is own property of a Boolean object that uses Object's [[GetOwnProperty]]
 */


function testcase() {
        var boolObj = new Boolean(false);

        boolObj.foo = 10;
        var preCheck = Object.isExtensible(boolObj);
        Object.seal(boolObj);

        delete boolObj.foo;
        return preCheck && boolObj.foo === 10;
    }
runTestCase(testcase);
