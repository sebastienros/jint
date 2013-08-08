/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-7.js
 * @description Object.seal - 'P' is own property of a Function object that uses Object's [[GetOwnProperty]]
 */


function testcase() {
        var funObj = function () { };

        funObj.foo = 10; // default [[Configurable]] attribute value of foo: true
        var preCheck = Object.isExtensible(funObj);
        Object.seal(funObj);

        delete funObj.foo;
        return preCheck && funObj.foo === 10;
    }
runTestCase(testcase);
