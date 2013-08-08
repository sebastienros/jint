/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-32-s.js
 * @description arguments as local var identifier defined twice and assigned once throws SyntaxError in strict mode
 * @onlyStrict
 */




function testcase() {
  'use strict';

  try {
    eval('function foo() { var arguments, arguments = 42;}');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
}
runTestCase(testcase);