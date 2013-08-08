/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-15.js
 * @description Object.seal - 'P' is own property of an Arguments object which implements its own [[GetOwnProperty]]
 */


function testcase() {
        var argObj = (function () { return arguments; })();

        argObj.foo = 10; // default [[Configurable]] attribute value of foo: true
        var preCheck = Object.isExtensible(argObj);
        Object.seal(argObj);

        delete argObj.foo;
        return preCheck && argObj.foo === 10;
    }
runTestCase(testcase);
