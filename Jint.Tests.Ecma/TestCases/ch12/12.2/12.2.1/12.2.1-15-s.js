/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-15-s.js
 * @description arguments - a function expr assigning into 'arguments' throws a SyntaxError in strict mode
 * @onlyStrict
 */




function testcase() {
  'use strict';

  try {
    eval('(function () {arguments = 42;})()');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
}
runTestCase(testcase);