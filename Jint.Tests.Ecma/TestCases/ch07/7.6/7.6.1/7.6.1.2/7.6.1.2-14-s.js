/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-14-s.js
 * @description Strict Mode - SyntaxError isn't thrown when 'implements0' occurs in strict mode code
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var implements0 = 1;
        return implements0 === 1;
    }
runTestCase(testcase);
