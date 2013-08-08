/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-34-s.js
 * @description 'for(var eval in ...) {...}' throws SyntaxError in strict mode
 * @onlyStrict
 */


function testcase() {
  'use strict';

  try {
    eval('for (var eval in null) {};');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
