/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-9-s.js
 * @description 'use strict' directive - may occur multiple times
 * @noStrict
 */


function testcase() {

  function foo()
  {
     'use strict';
     "use strict";
     return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
