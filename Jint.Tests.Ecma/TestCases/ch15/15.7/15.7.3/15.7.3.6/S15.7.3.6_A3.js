// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Number.POSITIVE_INFINITY is DontDelete
 *
 * @path ch15/15.7/15.7.3/15.7.3.6/S15.7.3.6_A3.js
 * @description Checking if deleting Number.POSITIVE_INFINITY fails
 * @noStrict
 */

// CHECK#1
if (delete Number.POSITIVE_INFINITY !== false) {
  $ERROR('#1: delete Number.POSITIVE_INFINITY === false');
}

