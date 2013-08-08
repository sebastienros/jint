/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-11-s.js
 * @description Strict Mode - SyntaxError isn't thrown when 'Implements' occurs in strict mode code
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var Implements = 1;
        return Implements === 1;
    }
runTestCase(testcase);
