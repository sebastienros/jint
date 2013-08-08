/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-20-s.js
 * @description StrictMode - enumerating over a function object looking for 'arguments' fails inside the function
 * @onlyStrict
 */



function testcase() {
            var foo = Function("'use strict'; for (var tempIndex in this) {if (tempIndex===\"arguments\") {return false;}}; return true;");
            return foo();
}
runTestCase(testcase);