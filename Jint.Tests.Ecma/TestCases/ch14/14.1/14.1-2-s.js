/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-2-s.js
 * @description "use strict" directive - correct usage double quotes
 * @noStrict
 */


function testcase() {

  function foo()
  {
    "use strict";
     return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
