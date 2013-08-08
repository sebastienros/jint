/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-15-s.js
 * @description StrictMode - enumerating over a function object looking for 'arguments' fails outside of the function
 * @onlyStrict
 */



function testcase() {
        var foo = new Function("'use strict';");
        
        for (var tempIndex in foo) {
            if (tempIndex === "arguments") {
                return false;
            }
        }
        return true;
}
runTestCase(testcase);